using Marketplace.Modules.Location.Contracts;
using Marketplace.Modules.Orders.Application;
using Marketplace.Modules.Orders.Domain;
using Marketplace.Modules.Orders.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Orders.Endpoints;

internal static class OrderEndpoints
{
    public sealed record UpdateTermsRequest(int Version, Handover? Handover, DateOnly? HandoverDate, PaymentMethod? PaymentMethod, string? Notes);
    public sealed record AcceptRequest(int Version);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1").WithTags("Orders").RequireAuthorization();
        group.MapGet("/me/orders", GetMyOrders);
        group.MapGet("/orders/{id:guid}", GetOrder);
        group.MapGet("/orders/{id:guid}/agreement", GetOrder);
        group.MapGet("/orders/by-auction/{auctionId:guid}", GetOrderByAuction);
        group.MapPatch("/orders/{id:guid}/agreement", UpdateTerms);
        group.MapPost("/orders/{id:guid}/agreement/accept", Accept);
        group.MapPost("/orders/{id:guid}/confirm-paid", ConfirmPaid);
        group.MapPost("/orders/{id:guid}/mark-received", MarkReceived);
        group.MapPost("/orders/{id:guid}/cancel", Cancel);
    }

    private static async Task<List<OrderSummary>> GetMyOrders(string? role, OrdersDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var me = current.RequireId();
        var orders = await db.Orders.AsNoTracking().Include(o => o.Agreement)
            .Where(o => (role != "seller" && o.BuyerId == me) || (role != "buyer" && o.SellerId == me))
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return orders.Select(o =>
        {
            var isBuyer = o.BuyerId == me;
            return new OrderSummary(o.Id, o.Agreement.Number, o.ListingId, o.Title, OrderViews.StatusOf(o.Status),
                isBuyer ? "buyer" : "seller", isBuyer ? o.SellerHandle : o.BuyerHandle, o.Currency, o.Total, o.CreatedAt);
        }).ToList();
    }

    private static async Task<OrderView> GetOrder(Guid id, OrdersDbContext db, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var order = await db.FindOrderAsync(id, ct) ?? throw ProblemException.NotFound("Order");
        return await ViewAsync(order, current.RequireId(), locations, clock, ct);
    }

    private static async Task<OrderView> GetOrderByAuction(Guid auctionId, OrdersDbContext db, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var id = await db.Orders.Where(o => o.AuctionId == auctionId).Select(o => (Guid?)o.Id).SingleOrDefaultAsync(ct)
            ?? throw ProblemException.NotFound("Order");
        var order = (await db.FindOrderAsync(id, ct))!;
        return await ViewAsync(order, current.RequireId(), locations, clock, ct);
    }

    private static async Task<OrderView> UpdateTerms(Guid id, UpdateTermsRequest request, OrderService orders, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var me = current.RequireId();
        var order = await orders.MutateAsync(id, me, "terms_changed",
            (o, now) => o.UpdateTerms(me, request.Version, request.Handover, request.HandoverDate, request.PaymentMethod, request.Notes, now), ct);
        return await ViewAsync(order, me, locations, clock, ct);
    }

    private static async Task<OrderView> Accept(Guid id, AcceptRequest request, OrderService orders, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var me = current.RequireId();
        var order = await orders.MutateAsync(id, me, "accepted", (o, now) => { o.Accept(me, request.Version, now); return true; }, ct);
        return await ViewAsync(order, me, locations, clock, ct);
    }

    private static async Task<OrderView> ConfirmPaid(Guid id, OrderService orders, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var me = current.RequireId();
        var order = await orders.MutateAsync(id, me, "confirmed_paid", (o, now) => { o.ConfirmPaid(me, now); return true; }, ct);
        return await ViewAsync(order, me, locations, clock, ct);
    }

    private static async Task<OrderView> MarkReceived(Guid id, OrderService orders, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var me = current.RequireId();
        var order = await orders.MutateAsync(id, me, "marked_received", (o, now) => { o.MarkReceived(me, now); return true; }, ct);
        return await ViewAsync(order, me, locations, clock, ct);
    }

    private static async Task<OrderView> Cancel(Guid id, OrderService orders, ILocationService locations, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var me = current.RequireId();
        var order = await orders.MutateAsync(id, me, "cancelled", (o, now) => { o.Cancel(me, now); return true; }, ct);
        return await ViewAsync(order, me, locations, clock, ct);
    }

    /// <summary>Only the two parties can read a deal; the exact pickup address appears once both accepted (feature design §9.2).</summary>
    private static async Task<OrderView> ViewAsync(Order order, Guid userId, ILocationService locations, IClock clock, CancellationToken ct)
    {
        var role = order.RequireParty(userId);
        var showAddress = order.IsAgreed && order.Agreement.Handover == Handover.Pickup;
        var address = showAddress ? await locations.GetPickupAddressAsync(order.Agreement.PickupLocationId, ct) : null;
        return OrderViews.From(order, role, address, clock.UtcNow);
    }
}
