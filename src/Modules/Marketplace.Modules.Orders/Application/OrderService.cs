using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Orders.Contracts;
using Marketplace.Modules.Orders.Domain;
using Marketplace.Modules.Orders.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Orders.Application;

/// <summary>Runs every change to an order under a row lock, then saves it with its OrderUpdated event.</summary>
internal sealed class OrderService(OrdersDbContext db, IClock clock)
{
    public async Task<Order> MutateAsync(Guid orderId, Guid actorId, string action, Func<Order, DateTimeOffset, bool> change, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize both parties' actions on one order (e.g. simultaneous accepts must end "agreed").
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM orders.\"order\" WHERE id = {orderId} FOR UPDATE", ct);
        var order = await db.FindOrderAsync(orderId, ct) ?? throw ProblemException.NotFound("Order");
        order.RequireParty(actorId);

        var now = clock.UtcNow;
        if (change(order, now))
            db.Publish(Updated(order, action, actorId), now);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return order;
    }

    public static OrderUpdated Updated(Order o, string action, Guid? actorId) => new(
        o.Id, o.ListingId, o.Agreement.Number, o.Title, o.BuyerId, o.SellerId,
        OrderViews.StatusOf(o.Status), o.Agreement.Version, action, actorId);
}

/// <summary>A sold auction (bid win or Buy Now) creates the order and its deal agreement (feature design §5.1, §6.1).</summary>
internal sealed class CreateOrderOnAuctionClosed(OrdersDbContext db, IListingQueries listings, IClock clock)
    : IntegrationEventHandler<AuctionClosed, OrdersDbContext>(db, clock)
{
    protected override async Task HandleAsync(AuctionClosed e, CancellationToken ct)
    {
        if (!e.Sold || e.WinnerId is not { } buyerId || e.FinalPrice is not { } price)
            return;
        if (await Db.Orders.AnyAsync(o => o.AuctionId == e.AuctionId, ct))
            return;

        var listing = await listings.GetSummaryAsync(e.ListingId, ct)
            ?? throw new InvalidOperationException($"Listing {e.ListingId} not found for auction {e.AuctionId}.");

        var order = Order.Create(e.AuctionId, listing, buyerId, e.WinnerHandle ?? "buyer", e.SellerHandle,
            price, e.Currency, await Db.NextAgreementNumberAsync(ct), Clock.UtcNow);
        Db.Orders.Add(order);
        Db.Publish(OrderService.Updated(order, "created", null), Clock.UtcNow);
    }
}
