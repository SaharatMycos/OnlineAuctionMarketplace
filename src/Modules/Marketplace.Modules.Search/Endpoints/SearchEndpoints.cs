using Marketplace.Modules.Search.Domain;
using Marketplace.Modules.Search.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Geo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Marketplace.Modules.Search.Endpoints;

internal static class SearchEndpoints
{
    /// <summary>
    /// A listing card. <c>DistanceKm</c> is computed from the fuzzed public point and rounded
    /// (0 means "under 1 km"); exact locations never leave the Location module.
    /// </summary>
    public sealed record CardResponse(
        Guid ListingId,
        Guid? AuctionId,
        string Title,
        string Emoji,
        string? ThumbnailUrl,
        string CategoryName,
        string Condition,
        string Delivery,
        decimal? ShippingCost,
        string AreaName,
        int? DistanceKm,
        bool? IsLocal,
        string Currency,
        decimal Price,
        int BidCount,
        decimal? BuyNowPrice,
        DateTimeOffset EndsAt,
        string Status,
        string SellerHandle);

    public sealed record FeedResponse(List<CardResponse> Near, List<CardResponse> ShipsToYou, List<CardResponse> EndingSoon);

    private const int MaxRadiusKm = 100;

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1").WithTags("Search");
        group.MapGet("/feed/nearby", Feed);
        group.MapGet("/search", Search);
    }

    /// <summary>Home feed (feature design §9.3): "Near you" in the radius, "Ships to you" beyond it, and ending soon among both.</summary>
    private static async Task<FeedResponse> Feed(double? lat, double? lng, int? radiusKm, SearchDbContext db, IClock clock, CancellationToken ct)
    {
        var origin = Origin(lat, lng);
        var radiusM = RadiusMetres(radiusKm);
        var live = Live(db, clock);

        if (origin is null)
        {
            var anywhere = await live.OrderBy(c => c.EndsAt).Take(24).ToListAsync(ct);
            var cards = anywhere.Select(c => ToResponse(c, null, null)).ToList();
            return new FeedResponse([], cards.Where(c => c.Delivery != "pickup").ToList(), cards.Take(8).ToList());
        }

        var near = radiusM is null
            ? await WithDistance(live, origin).OrderBy(x => x.Metres).ThenBy(x => x.Card.EndsAt).Take(24).ToListAsync(ct)
            : await WithDistance(live.Where(c => c.PublicGeo.IsWithinDistance(origin, radiusM.Value)), origin)
                .OrderBy(x => x.Metres).ThenBy(x => x.Card.EndsAt).Take(24).ToListAsync(ct);

        var ships = radiusM is null
            ? []
            : await WithDistance(live.Where(c => c.Delivery != "pickup" && !c.PublicGeo.IsWithinDistance(origin, radiusM.Value)), origin)
                .OrderBy(x => x.Card.EndsAt).Take(24).ToListAsync(ct);

        var nearCards = near.Select(x => ToResponse(x.Card, x.Metres, true)).ToList();
        var shipCards = ships.Select(x => ToResponse(x.Card, x.Metres, false)).ToList();
        var endingSoon = nearCards.Concat(shipCards).OrderBy(c => c.EndsAt).Take(8).ToList();
        return new FeedResponse(nearCards, shipCards, endingSoon);
    }

    /// <summary>
    /// Search (feature design §9.4). Pickup-only items outside the radius are never returned; items
    /// that ship are included unless <paramref name="includeShipping"/> is false.
    /// </summary>
    private static async Task<List<CardResponse>> Search(
        string? q, int? categoryId, string? delivery, double? lat, double? lng, int? radiusKm,
        bool? includeShipping, string? sort, string? status, SearchDbContext db, IClock clock, CancellationToken ct)
    {
        var origin = Origin(lat, lng);
        var radiusM = RadiusMetres(radiusKm);
        var shipping = includeShipping ?? true;

        var query = status == "ended"
            ? db.ListingCards.AsNoTracking().Where(c => c.Status != "live")
            : Live(db, clock);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => EF.Functions.ILike(c.Title, $"%{q.Trim()}%"));
        if (categoryId is { } category)
            query = query.Where(c => c.CategoryId == category);
        if (delivery == "pickup")
            query = query.Where(c => c.Delivery != "ship");
        else if (delivery == "ship")
            query = query.Where(c => c.Delivery != "pickup");

        if (origin is not null && radiusM is { } r)
        {
            query = delivery == "pickup" || !shipping
                ? query.Where(c => c.PublicGeo.IsWithinDistance(origin, r))
                : query.Where(c => c.PublicGeo.IsWithinDistance(origin, r) || c.Delivery != "pickup");
        }

        sort ??= origin is not null && string.IsNullOrWhiteSpace(q) ? "nearest" : "ending";
        if (origin is null)
        {
            var cards = await Sort(query, sort).Take(48).ToListAsync(ct);
            return cards.Select(c => ToResponse(c, null, null)).ToList();
        }

        var withDistance = WithDistance(query, origin);
        withDistance = sort switch
        {
            "nearest" => withDistance.OrderBy(x => x.Metres).ThenBy(x => x.Card.EndsAt),
            "price_asc" => withDistance.OrderBy(x => x.Card.Price),
            "price_desc" => withDistance.OrderByDescending(x => x.Card.Price),
            "bids" => withDistance.OrderByDescending(x => x.Card.BidCount),
            "newest" => withDistance.OrderByDescending(x => x.Card.PublishedAt),
            _ => withDistance.OrderBy(x => x.Card.EndsAt),
        };
        var results = await withDistance.Take(48).ToListAsync(ct);
        return results.Select(x => ToResponse(x.Card, x.Metres, radiusM is null ? null : x.Metres <= radiusM)).ToList();
    }

    private static IQueryable<ListingCard> Live(SearchDbContext db, IClock clock)
    {
        var now = clock.UtcNow;
        return db.ListingCards.AsNoTracking().Where(c => c.Status == "live" && c.EndsAt > now);
    }

    private static IQueryable<ListingCard> Sort(IQueryable<ListingCard> query, string sort) => sort switch
    {
        "price_asc" => query.OrderBy(c => c.Price),
        "price_desc" => query.OrderByDescending(c => c.Price),
        "bids" => query.OrderByDescending(c => c.BidCount),
        "newest" => query.OrderByDescending(c => c.PublishedAt),
        _ => query.OrderBy(c => c.EndsAt),
    };

    /// <summary>Member-initialised (not positional) so EF can order by <see cref="Metres"/> in SQL.</summary>
    private sealed class CardWithDistance
    {
        public required ListingCard Card { get; init; }
        public double Metres { get; init; }
    }

    private static IQueryable<CardWithDistance> WithDistance(IQueryable<ListingCard> query, Point origin) =>
        query.Select(c => new CardWithDistance { Card = c, Metres = c.PublicGeo.Distance(origin) });

    private static Point? Origin(double? lat, double? lng) =>
        lat is { } la && lng is { } lo ? GeoPoints.Create(la, lo) : null;

    private static double? RadiusMetres(int? radiusKm) => radiusKm switch
    {
        null or <= 0 => null,
        > MaxRadiusKm => throw ProblemException.BadRequest("invalid_radius", $"The radius can be at most {MaxRadiusKm} km; leave it out for anywhere."),
        { } km => km * 1000d,
    };

    private static CardResponse ToResponse(ListingCard c, double? metres, bool? isLocal) => new(
        c.ListingId, c.AuctionId, c.Title, c.Emoji, c.ThumbnailUrl, c.CategoryName, c.Condition, c.Delivery, c.ShippingCost,
        c.AreaName, metres is { } m ? PublicGrid.RoundDistanceKm(m) : null, isLocal,
        c.Currency, c.Price, c.BidCount, c.BuyNowPrice, c.EndsAt, c.Status, c.SellerHandle);
}
