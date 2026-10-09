using Marketplace.Modules.Catalog.Contracts;
using Marketplace.SharedKernel;
using NetTopologySuite.Geometries;

namespace Marketplace.Modules.Catalog.Domain;

internal sealed class Category
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Emoji { get; init; }
}

internal enum ListingStatus
{
    Draft,
    Published,
    Ended,
}

/// <summary>An item for sale by English auction (feature design §2, §3). Fixed price and Make Offer come later.</summary>
internal sealed class Listing
{
    /// <summary>1, 3, 5, 7, 10 days (feature design §3.1) plus 5 min and 1 h for demos and testing.</summary>
    public static readonly int[] AllowedDurationsMinutes = [5, 60, 1440, 4320, 7200, 10080, 14400];

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid SellerId { get; init; }
    public required string SellerHandle { get; init; }
    public int CategoryId { get; set; }
    public Category? Category { get; private set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Condition { get; set; }
    public List<string> PhotoUrls { get; set; } = [];
    public Dictionary<string, string> Specs { get; set; } = [];
    public DeliveryOption Delivery { get; set; }
    public decimal? ShippingCost { get; set; }

    /// <summary>The private location lives in the Location module; we keep only its id and public-grade snapshot.</summary>
    public Guid LocationId { get; private set; }
    public Point? PublicGeo { get; private set; }
    public string AreaName { get; private set; } = "";

    public required string Currency { get; init; }
    public decimal StartPrice { get; set; }
    /// <summary>Hidden from buyers; they only see "reserve met / not met".</summary>
    public decimal? ReservePrice { get; set; }
    public decimal? BuyNowPrice { get; set; }
    public int DurationMinutes { get; set; }

    public ListingStatus Status { get; private set; } = ListingStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }

    public void EnsureEditableBy(Guid userId)
    {
        if (userId != SellerId)
            throw ProblemException.Forbidden("not_seller", "Only the seller can change this listing.");
        if (Status != ListingStatus.Draft)
            throw ProblemException.Conflict("not_draft", "Only drafts can be edited.");
    }

    public void Validate()
    {
        if (Title.Trim().Length is < 5 or > 80)
            throw ProblemException.BadRequest("invalid_title", "The title must be 5–80 characters.");
        if (Description.Length > 4000)
            throw ProblemException.BadRequest("invalid_description", "The description must be at most 4,000 characters.");
        if (StartPrice <= 0 || decimal.Round(StartPrice, 2) != StartPrice)
            throw ProblemException.BadRequest("invalid_start_price", "The start price must be positive, with at most 2 decimals.");
        if (ReservePrice is { } reserve && (reserve < StartPrice || decimal.Round(reserve, 2) != reserve))
            throw ProblemException.BadRequest("invalid_reserve_price", "The reserve price must be at least the start price.");
        if (BuyNowPrice is { } buyNow && (buyNow <= Math.Max(StartPrice, ReservePrice ?? 0) || decimal.Round(buyNow, 2) != buyNow))
            throw ProblemException.BadRequest("invalid_buy_now_price", "Buy Now must be higher than the start and reserve prices.");
        if (ShippingCost is < 0)
            throw ProblemException.BadRequest("invalid_shipping_cost", "Shipping cost can't be negative.");
        if (!AllowedDurationsMinutes.Contains(DurationMinutes))
            throw ProblemException.BadRequest("invalid_duration", "Pick 5 min, 1 h, or 1, 3, 5, 7 or 10 days.");
        if (PhotoUrls.Count > 24 || PhotoUrls.Any(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw ProblemException.BadRequest("invalid_photos", "Up to 24 photo URLs (http or https).");
    }

    /// <summary>Links the listing to its (private) item location, keeping only the public-grade point and area.</summary>
    public void PlaceAt(Guid locationId, Point publicGeo, string areaName) =>
        (LocationId, PublicGeo, AreaName) = (locationId, publicGeo, areaName);

    public void Publish(DateTimeOffset now)
    {
        if (Status != ListingStatus.Draft)
            throw ProblemException.Conflict("already_published", "This listing is already published.");
        if (PublicGeo is null)
            throw ProblemException.BadRequest("location_required", "Set the item location before publishing.");
        Validate();
        Status = ListingStatus.Published;
        PublishedAt = now;
        EndsAt = now.AddMinutes(DurationMinutes);
    }

    public void End()
    {
        if (Status == ListingStatus.Published)
            Status = ListingStatus.Ended;
    }
}
