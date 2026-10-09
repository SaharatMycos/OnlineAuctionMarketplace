using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Auctions.Domain;
using Marketplace.Modules.Auctions.Persistence;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Auctions.Application;

/// <summary>A published listing becomes a live English auction.</summary>
internal sealed class StartAuctionOnListingPublished(AuctionsDbContext db, IClock clock)
    : IntegrationEventHandler<ListingPublished, AuctionsDbContext>(db, clock)
{
    protected override async Task HandleAsync(ListingPublished e, CancellationToken ct)
    {
        if (await Db.Auctions.AnyAsync(a => a.ListingId == e.ListingId, ct))
            return;

        var auction = Auction.Start(e.ListingId, e.SellerId, e.SellerHandle, e.Title, e.Currency,
            e.StartPrice, e.ReservePrice, e.BuyNowPrice, e.StartsAt, e.EndsAt);
        Db.Auctions.Add(auction);
        Db.Publish(new AuctionStarted(auction.Id, auction.ListingId, auction.StartPrice, auction.EndsAt, auction.BuyNowAvailable), Clock.UtcNow);
    }
}
