using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Catalog.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Catalog.Application;

/// <summary>When its auction closes, the listing ends.</summary>
internal sealed class EndListingOnAuctionClosed(CatalogDbContext db, IClock clock)
    : IntegrationEventHandler<AuctionClosed, CatalogDbContext>(db, clock)
{
    protected override async Task HandleAsync(AuctionClosed @event, CancellationToken ct)
    {
        var listing = await Db.Listings.SingleOrDefaultAsync(l => l.Id == @event.ListingId, ct);
        listing?.End();
    }
}

internal sealed class ListingQueries(CatalogDbContext db) : IListingQueries
{
    public Task<ListingSummary?> GetSummaryAsync(Guid listingId, CancellationToken ct) =>
        db.Listings
            .Where(l => l.Id == listingId)
            .Select(l => new ListingSummary(l.Id, l.SellerId, l.Title, l.Delivery, l.ShippingCost, l.LocationId, l.AreaName))
            .SingleOrDefaultAsync(ct);
}
