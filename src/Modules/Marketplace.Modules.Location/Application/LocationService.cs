using Marketplace.Modules.Location.Contracts;
using Marketplace.Modules.Location.Domain;
using Marketplace.Modules.Location.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Geo;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Location.Application;

internal sealed class LocationService(LocationDbContext db, IClock clock) : ILocationService
{
    public async Task<PublicItemLocation> CreateItemLocationAsync(Guid ownerId, ItemLocationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            throw ProblemException.BadRequest("address_required", "Give the pickup address (it stays private).");

        double lat, lng;
        string area;
        if (request.Latitude is { } la && request.Longitude is { } lo)
        {
            (lat, lng) = (la, lo);
            area = request.AreaName?.Trim() is { Length: > 0 } name ? name : await NearestAreaAsync(la, lo, ct);
        }
        else if (request.PlaceId is { } placeId)
        {
            var place = await db.Places.SingleOrDefaultAsync(p => p.Id == placeId, ct) ?? throw ProblemException.NotFound("Place");
            (lat, lng, area) = (place.Latitude, place.Longitude, place.Name);
        }
        else
        {
            throw ProblemException.BadRequest("location_required", "Pick an area or share coordinates.");
        }

        var location = new ItemLocation
        {
            OwnerId = ownerId,
            Address = request.Address.Trim(),
            Geo = GeoPoints.Create(lat, lng),
            PublicGeo = PublicGrid.Snap(lat, lng),
            AreaName = area,
            CreatedAt = clock.UtcNow,
        };
        db.ItemLocations.Add(location);
        await db.SaveChangesAsync(ct);

        return new PublicItemLocation(location.Id, location.PublicGeo.Latitude(), location.PublicGeo.Longitude(), location.AreaName);
    }

    public async Task<string?> GetPickupAddressAsync(Guid locationId, CancellationToken ct) =>
        await db.ItemLocations.Where(l => l.Id == locationId).Select(l => l.Address).SingleOrDefaultAsync(ct);

    /// <summary>Area name for raw coordinates: the closest gazetteer area within 15 km, else "Near &lt;city&gt;".</summary>
    public async Task<string> NearestAreaAsync(double lat, double lng, CancellationToken ct)
    {
        var places = await db.Places.ToListAsync(ct);
        var nearest = places
            .Select(p => (Place: p, Km: HaversineKm(lat, lng, p.Latitude, p.Longitude)))
            .OrderBy(x => x.Place.Kind == "area" ? 0 : 1).ThenBy(x => x.Km)
            .FirstOrDefault(x => x.Km <= 15);
        if (nearest.Place is not null)
            return nearest.Place.Name;
        var city = places.MinBy(p => HaversineKm(lat, lng, p.Latitude, p.Longitude))!;
        return $"Near {city.City}";
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }
}
