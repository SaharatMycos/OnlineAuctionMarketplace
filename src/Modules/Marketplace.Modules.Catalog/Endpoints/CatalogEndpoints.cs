using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Catalog.Domain;
using Marketplace.Modules.Catalog.Persistence;
using Marketplace.Modules.Location.Contracts;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Auth;
using Marketplace.SharedKernel.Geo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Marketplace.Modules.Catalog.Endpoints;

internal static class CatalogEndpoints
{
    public sealed record LocationInput(int? PlaceId, double? Lat, double? Lng, string Address, string? AreaName);

    public sealed record ListingInput(
        int CategoryId,
        string Title,
        string Description,
        string Condition,
        List<string>? PhotoUrls,
        Dictionary<string, string>? Specs,
        DeliveryOption Delivery,
        decimal? ShippingCost,
        decimal StartPrice,
        decimal? ReservePrice,
        decimal? BuyNowPrice,
        int DurationMinutes);

    public sealed record CreateListingRequest(ListingInput Listing, LocationInput Location, bool Publish = false);

    public sealed record CategoryResponse(int Id, string Name, string Slug, string Emoji);

    /// <summary>Public listing view. The reserve price appears only for the seller; there's never an exact location.</summary>
    public sealed record ListingResponse(
        Guid Id,
        string SellerHandle,
        bool IsMine,
        CategoryResponse Category,
        string Title,
        string Description,
        string Condition,
        List<string> PhotoUrls,
        Dictionary<string, string> Specs,
        DeliveryOption Delivery,
        decimal? ShippingCost,
        string AreaName,
        double PublicLat,
        double PublicLng,
        string Currency,
        decimal StartPrice,
        decimal? ReservePrice,
        decimal? BuyNowPrice,
        int DurationMinutes,
        string Status,
        DateTimeOffset? PublishedAt,
        DateTimeOffset? EndsAt);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1").WithTags("Catalog");
        group.MapGet("/categories", GetCategories);
        group.MapGet("/listings/{id:guid}", GetListing);
        group.MapPost("/listings", CreateListing).RequireAuthorization();
        group.MapPut("/listings/{id:guid}", UpdateListing).RequireAuthorization();
        group.MapPost("/listings/{id:guid}/publish", PublishListing).RequireAuthorization();
        group.MapGet("/me/listings", GetMyListings).RequireAuthorization();
    }

    private static Task<List<CategoryResponse>> GetCategories(CatalogDbContext db, CancellationToken ct) =>
        db.Categories.OrderBy(c => c.Id).Select(c => new CategoryResponse(c.Id, c.Name, c.Slug, c.Emoji)).ToListAsync(ct);

    private static async Task<ListingResponse> GetListing(Guid id, CatalogDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var listing = await db.Listings.Include(l => l.Category).SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ProblemException.NotFound("Listing");
        var isMine = current.Id == listing.SellerId;
        if (listing.Status == ListingStatus.Draft && !isMine)
            throw ProblemException.NotFound("Listing");
        return ToResponse(listing, isMine);
    }

    private static async Task<IResult> CreateListing(
        CreateListingRequest request, CatalogDbContext db, ILocationService locations, ICurrentUser current,
        IConfiguration config, IClock clock, CancellationToken ct)
    {
        var sellerId = current.RequireId();
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.Listing.CategoryId, ct)
            ?? throw ProblemException.BadRequest("invalid_category", "Pick a category.");

        var listing = new Listing
        {
            SellerId = sellerId,
            SellerHandle = current.Handle ?? "seller",
            Title = "",
            Description = "",
            Condition = "",
            Currency = config["Marketplace:Currency"] ?? "USD",
            CreatedAt = clock.UtcNow,
        };
        Apply(listing, request.Listing, category.Id);
        listing.Validate();

        var loc = request.Location;
        // Location owns the private address and exact point; the listing keeps the public-grade snapshot.
        var location = await locations.CreateItemLocationAsync(sellerId, new ItemLocationRequest(loc.PlaceId, loc.Lat, loc.Lng, loc.Address, loc.AreaName), ct);
        listing.PlaceAt(location.LocationId, GeoPoints.Create(location.Latitude, location.Longitude), location.AreaName);

        db.Listings.Add(listing);
        if (request.Publish)
            Publish(db, listing, category, clock);
        await db.SaveChangesAsync(ct);

        await db.Entry(listing).Reference(l => l.Category).LoadAsync(ct);
        return Results.Created($"/v1/listings/{listing.Id}", ToResponse(listing, isMine: true));
    }

    private static async Task<ListingResponse> UpdateListing(Guid id, ListingInput input, CatalogDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw ProblemException.NotFound("Listing");
        listing.EnsureEditableBy(current.RequireId());
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId, ct))
            throw ProblemException.BadRequest("invalid_category", "Pick a category.");
        Apply(listing, input, input.CategoryId);
        listing.Validate();
        await db.SaveChangesAsync(ct);
        await db.Entry(listing).Reference(l => l.Category).LoadAsync(ct);
        return ToResponse(listing, isMine: true);
    }

    private static async Task<ListingResponse> PublishListing(Guid id, CatalogDbContext db, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var listing = await db.Listings.Include(l => l.Category).SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ProblemException.NotFound("Listing");
        if (listing.SellerId != current.RequireId())
            throw ProblemException.Forbidden("not_seller", "Only the seller can publish this listing.");
        Publish(db, listing, listing.Category!, clock);
        await db.SaveChangesAsync(ct);
        return ToResponse(listing, isMine: true);
    }

    private static async Task<List<ListingResponse>> GetMyListings(CatalogDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var me = current.RequireId();
        var listings = await db.Listings.Include(l => l.Category)
            .Where(l => l.SellerId == me)
            .OrderByDescending(l => l.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return listings.Select(l => ToResponse(l, isMine: true)).ToList();
    }

    /// <summary>Publishing and its event commit together (transactional outbox).</summary>
    private static void Publish(CatalogDbContext db, Listing listing, Category category, IClock clock)
    {
        var now = clock.UtcNow;
        listing.Publish(now);
        db.Publish(new ListingPublished(
            listing.Id, listing.SellerId, listing.SellerHandle, listing.Title,
            category.Id, category.Name, category.Emoji, listing.PhotoUrls.FirstOrDefault(), listing.Condition,
            listing.Delivery, listing.ShippingCost,
            listing.PublicGeo!.Latitude(), listing.PublicGeo!.Longitude(), listing.AreaName,
            listing.Currency, listing.StartPrice, listing.ReservePrice, listing.BuyNowPrice,
            listing.PublishedAt!.Value, listing.EndsAt!.Value), now);
    }

    private static void Apply(Listing listing, ListingInput input, int categoryId)
    {
        listing.CategoryId = categoryId;
        listing.Title = input.Title?.Trim() ?? "";
        listing.Description = input.Description?.Trim() ?? "";
        listing.Condition = string.IsNullOrWhiteSpace(input.Condition) ? "Used" : input.Condition.Trim();
        listing.PhotoUrls = input.PhotoUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList() ?? [];
        listing.Specs = input.Specs?.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)).ToDictionary(kv => kv.Key.Trim(), kv => kv.Value?.Trim() ?? "") ?? [];
        listing.Delivery = input.Delivery;
        listing.ShippingCost = input.Delivery == DeliveryOption.Pickup ? null : input.ShippingCost ?? 0;
        listing.StartPrice = input.StartPrice;
        listing.ReservePrice = input.ReservePrice;
        listing.BuyNowPrice = input.BuyNowPrice;
        listing.DurationMinutes = input.DurationMinutes;
    }

    private static ListingResponse ToResponse(Listing l, bool isMine) => new(
        l.Id, l.SellerHandle, isMine,
        new CategoryResponse(l.Category!.Id, l.Category.Name, l.Category.Slug, l.Category.Emoji),
        l.Title, l.Description, l.Condition, l.PhotoUrls, l.Specs, l.Delivery, l.ShippingCost,
        l.AreaName, l.PublicGeo?.Latitude() ?? 0, l.PublicGeo?.Longitude() ?? 0,
        l.Currency, l.StartPrice, isMine ? l.ReservePrice : null, l.BuyNowPrice, l.DurationMinutes,
        l.Status.ToString().ToLowerInvariant(), l.PublishedAt, l.EndsAt);
}
