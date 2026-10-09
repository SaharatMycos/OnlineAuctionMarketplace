using Marketplace.Modules.Location.Application;
using Marketplace.Modules.Location.Domain;
using Marketplace.Modules.Location.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Auth;
using Marketplace.SharedKernel.Geo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Location.Endpoints;

internal static class LocationEndpoints
{
    public sealed record PlaceResponse(int Id, string Name, string City, string Kind, double Lat, double Lng);
    /// <param name="RadiusKm">5, 10, 25, 50 or 100; null means anywhere.</param>
    public sealed record SetLocationRequest(int? PlaceId, double? Lat, double? Lng, int? RadiusKm, bool Anywhere = false);
    public sealed record MyLocationResponse(string AreaName, double Lat, double Lng, int? RadiusKm, string Source);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/geo/geocode", Geocode).WithTags("Location");
        app.MapGet("/v1/me/location", GetMine).RequireAuthorization().WithTags("Location");
        app.MapPut("/v1/me/location", SetMine).RequireAuthorization().WithTags("Location");
    }

    /// <summary>City/area search over the gazetteer. A vendor geocoder replaces this later (system design §4).</summary>
    private static async Task<List<PlaceResponse>> Geocode(string? q, LocationDbContext db, CancellationToken ct)
    {
        var query = db.Places.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{q.Trim()}%") || EF.Functions.ILike(p.City, $"%{q.Trim()}%"));
        return await query
            .OrderBy(p => p.Kind).ThenBy(p => p.Name)
            .Take(20)
            .Select(p => new PlaceResponse(p.Id, p.Name, p.City, p.Kind, p.Latitude, p.Longitude))
            .ToListAsync(ct);
    }

    private static async Task<IResult> GetMine(ICurrentUser current, LocationDbContext db, CancellationToken ct)
    {
        var mine = await db.UserLocations.SingleOrDefaultAsync(l => l.UserId == current.RequireId(), ct);
        return mine is null ? Results.NoContent() : Results.Ok(ToResponse(mine));
    }

    /// <summary>Stores only a public-grade point (snapped to the ~1 km grid), never raw GPS.</summary>
    private static async Task<MyLocationResponse> SetMine(
        SetLocationRequest request, ICurrentUser current, LocationDbContext db, LocationService locations, IClock clock, CancellationToken ct)
    {
        var userId = current.RequireId();
        int? radius = request.Anywhere ? null : request.RadiusKm ?? UserLocation.DefaultRadiusKm;
        if (radius is { } r && !UserLocation.AllowedRadiiKm.Contains(r))
            throw ProblemException.BadRequest("invalid_radius", $"Radius must be one of {string.Join(", ", UserLocation.AllowedRadiiKm)} km, or anywhere.");

        double lat, lng;
        string area, source;
        if (request.Lat is { } la && request.Lng is { } lo)
            (lat, lng, area, source) = (la, lo, await locations.NearestAreaAsync(la, lo, ct), "gps");
        else if (request.PlaceId is { } placeId)
        {
            var place = await db.Places.SingleOrDefaultAsync(p => p.Id == placeId, ct) ?? throw ProblemException.NotFound("Place");
            (lat, lng, area, source) = (place.Latitude, place.Longitude, place.Name, "manual");
        }
        else throw ProblemException.BadRequest("location_required", "Pick a place or share coordinates.");

        var point = PublicGrid.Snap(lat, lng);
        var mine = await db.UserLocations.SingleOrDefaultAsync(l => l.UserId == userId, ct);
        if (mine is null)
        {
            mine = new UserLocation { UserId = userId, PublicGeo = point, AreaName = area, Source = source };
            db.UserLocations.Add(mine);
        }
        (mine.PublicGeo, mine.AreaName, mine.Source, mine.RadiusKm, mine.UpdatedAt) = (point, area, source, radius, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return ToResponse(mine);
    }

    private static MyLocationResponse ToResponse(UserLocation l) =>
        new(l.AreaName, l.PublicGeo.Latitude(), l.PublicGeo.Longitude(), l.RadiusKm, l.Source);
}
