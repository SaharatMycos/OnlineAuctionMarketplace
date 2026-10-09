using NetTopologySuite.Geometries;

namespace Marketplace.Modules.Search.Domain;

/// <summary>
/// Read model for discovery (feature design §9.3–9.4): one row per published listing, combining
/// Catalog data with live auction state. Built only from integration events; holds public-grade
/// location data only. OpenSearch can replace the queries later without changing the API.
/// </summary>
internal sealed class ListingCard
{
    public Guid ListingId { get; init; }
    public Guid? AuctionId { get; set; }
    public required string SellerHandle { get; init; }
    public required string Title { get; init; }
    public int CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string Emoji { get; init; }
    public string? ThumbnailUrl { get; init; }
    public required string Condition { get; init; }
    /// <summary>pickup, both or ship.</summary>
    public required string Delivery { get; init; }
    public decimal? ShippingCost { get; init; }
    public required Point PublicGeo { get; init; }
    public required string AreaName { get; init; }
    public required string Currency { get; init; }
    public decimal Price { get; set; }
    public int BidCount { get; set; }
    public decimal? BuyNowPrice { get; set; }
    public DateTimeOffset PublishedAt { get; init; }
    public DateTimeOffset EndsAt { get; set; }
    /// <summary>live, sold or unsold.</summary>
    public string Status { get; set; } = "live";
    /// <summary>Last auction seq applied, so late or replayed events never move the card backwards.</summary>
    public long LastSeq { get; set; }
}
