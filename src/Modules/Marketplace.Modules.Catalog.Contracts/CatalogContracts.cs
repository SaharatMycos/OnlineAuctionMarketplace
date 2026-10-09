using Marketplace.SharedKernel.Events;

namespace Marketplace.Modules.Catalog.Contracts;

/// <summary>How the item changes hands (feature design §9.2).</summary>
public enum DeliveryOption
{
    Pickup,
    Both,
    Ship,
}

/// <summary>
/// A listing went live. Auctions creates its auction from this; Search builds the listing card.
/// Contains the hidden reserve price, so it must never be pushed to browsers.
/// </summary>
[IntegrationEvent("catalog.listing_published.v1")]
public sealed record ListingPublished(
    Guid ListingId,
    Guid SellerId,
    string SellerHandle,
    string Title,
    int CategoryId,
    string CategoryName,
    string CategoryEmoji,
    string? ThumbnailUrl,
    string Condition,
    DeliveryOption Delivery,
    decimal? ShippingCost,
    double PublicLatitude,
    double PublicLongitude,
    string AreaName,
    string Currency,
    decimal StartPrice,
    decimal? ReservePrice,
    decimal? BuyNowPrice,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt) : IIntegrationEvent;

public sealed record ListingSummary(
    Guid ListingId,
    Guid SellerId,
    string Title,
    DeliveryOption Delivery,
    decimal? ShippingCost,
    Guid LocationId,
    string AreaName);

public interface IListingQueries
{
    Task<ListingSummary?> GetSummaryAsync(Guid listingId, CancellationToken ct);
}
