using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Search.Domain;
using Marketplace.Modules.Search.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Geo;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Search.Application;

internal sealed class AddCardOnListingPublished(SearchDbContext db, IClock clock)
    : IntegrationEventHandler<ListingPublished, SearchDbContext>(db, clock)
{
    protected override async Task HandleAsync(ListingPublished e, CancellationToken ct)
    {
        if (await Db.ListingCards.AnyAsync(c => c.ListingId == e.ListingId, ct))
            return;
        Db.ListingCards.Add(new ListingCard
        {
            ListingId = e.ListingId,
            SellerHandle = e.SellerHandle,
            Title = e.Title,
            CategoryId = e.CategoryId,
            CategoryName = e.CategoryName,
            Emoji = e.CategoryEmoji,
            ThumbnailUrl = e.ThumbnailUrl,
            Condition = e.Condition,
            Delivery = e.Delivery.ToString().ToLowerInvariant(),
            ShippingCost = e.ShippingCost,
            PublicGeo = GeoPoints.Create(e.PublicLatitude, e.PublicLongitude),
            AreaName = e.AreaName,
            Currency = e.Currency,
            Price = e.StartPrice,
            BuyNowPrice = e.BuyNowPrice,
            PublishedAt = e.StartsAt,
            EndsAt = e.EndsAt,
        });
    }
}

/// <summary>Base for auction-driven updates: the card must exist (retry until the listing event lands) and seq must move forward.</summary>
internal abstract class CardUpdate<TEvent>(SearchDbContext db, IClock clock) : IntegrationEventHandler<TEvent, SearchDbContext>(db, clock)
    where TEvent : IIntegrationEvent
{
    protected async Task<ListingCard> CardAsync(Guid listingId, CancellationToken ct) =>
        await Db.ListingCards.SingleOrDefaultAsync(c => c.ListingId == listingId, ct)
        ?? throw new InvalidOperationException($"Listing card {listingId} doesn't exist yet; will retry.");
}

internal sealed class LinkCardOnAuctionStarted(SearchDbContext db, IClock clock) : CardUpdate<AuctionStarted>(db, clock)
{
    protected override async Task HandleAsync(AuctionStarted e, CancellationToken ct)
    {
        var card = await CardAsync(e.ListingId, ct);
        card.AuctionId = e.AuctionId;
        card.EndsAt = e.EndsAt;
        card.BuyNowPrice = e.BuyNowAvailable ? card.BuyNowPrice : null;
    }
}

internal sealed class UpdateCardOnBidPlaced(SearchDbContext db, IClock clock) : CardUpdate<BidPlaced>(db, clock)
{
    protected override async Task HandleAsync(BidPlaced e, CancellationToken ct)
    {
        var card = await CardAsync(e.ListingId, ct);
        if (e.Seq <= card.LastSeq)
            return;
        (card.Price, card.BidCount, card.EndsAt, card.LastSeq) = (e.Price, e.BidCount, e.EndsAt, e.Seq);
        if (!e.BuyNowAvailable)
            card.BuyNowPrice = null;
    }
}

internal sealed class UpdateCardOnAuctionClosed(SearchDbContext db, IClock clock) : CardUpdate<AuctionClosed>(db, clock)
{
    protected override async Task HandleAsync(AuctionClosed e, CancellationToken ct)
    {
        var card = await CardAsync(e.ListingId, ct);
        if (e.Seq < card.LastSeq)
            return;
        card.Status = e.Sold ? "sold" : "unsold";
        card.Price = e.FinalPrice ?? card.Price;
        card.BuyNowPrice = null;
        card.LastSeq = e.Seq;
    }
}
