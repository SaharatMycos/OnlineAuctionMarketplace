using Marketplace.Modules.Orders.Domain;

namespace Marketplace.Modules.Orders.Application;

internal sealed record AgreementView(
    string Number,
    int Version,
    decimal AgreedPrice,
    string Handover,
    DateOnly HandoverDate,
    string PaymentMethod,
    string Notes,
    string AreaName,
    /// <summary>Only after both parties accepted (feature design §9.2); null otherwise.</summary>
    string? PickupAddress,
    DateTimeOffset? BuyerAcceptedAt,
    DateTimeOffset? SellerAcceptedAt,
    DateTimeOffset? BuyerPaidAt,
    DateTimeOffset? SellerReceivedAt,
    DateTimeOffset? ReceivedAt);

internal sealed record DealEventView(string Action, string? By, int Version, string? Detail, DateTimeOffset At);

internal sealed record OrderView(
    Guid Id,
    Guid AuctionId,
    Guid ListingId,
    string Title,
    string Status,
    string Role,
    string BuyerHandle,
    string SellerHandle,
    string Currency,
    string Delivery,
    decimal ItemPrice,
    decimal? ShippingCost,
    decimal Total,
    DateTimeOffset AcceptBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool CanCancel,
    AgreementView Agreement,
    List<DealEventView> History);

internal sealed record OrderSummary(
    Guid Id,
    string Number,
    Guid ListingId,
    string Title,
    string Status,
    string Role,
    string Counterparty,
    string Currency,
    decimal Total,
    DateTimeOffset CreatedAt);

internal static class OrderViews
{
    public static string StatusOf(OrderStatus s) => s switch
    {
        OrderStatus.AgreementPending => "agreement_pending",
        OrderStatus.Agreed => "agreed",
        OrderStatus.DealPaid => "deal_paid",
        OrderStatus.Received => "received",
        _ => "cancelled",
    };

    private static string Camel(Enum value)
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static OrderView From(Order o, PartyRole role, string? pickupAddress, DateTimeOffset now)
    {
        var a = o.Agreement;
        string? By(Guid? actor) => actor is null ? "system" : actor == o.BuyerId ? "buyer" : actor == o.SellerId ? "seller" : null;
        return new OrderView(
            o.Id, o.AuctionId, o.ListingId, o.Title, StatusOf(o.Status), Camel(role), o.BuyerHandle, o.SellerHandle,
            o.Currency, Camel(o.Delivery), a.AgreedPrice, a.Handover == Handover.Shipping ? o.ShippingCost : null, o.Total,
            o.AcceptBy, o.CreatedAt, o.CompletedAt, role == PartyRole.Seller && o.CanCancel(now),
            new AgreementView(a.Number, a.Version, a.AgreedPrice, Camel(a.Handover), a.HandoverDate, Camel(a.PaymentMethod),
                a.Notes, a.AreaName, pickupAddress, a.BuyerAcceptedAt, a.SellerAcceptedAt, a.BuyerPaidAt, a.SellerReceivedAt, a.ReceivedAt),
            o.Events.OrderBy(e => e.At).Select(e => new DealEventView(e.Action, By(e.ActorId), e.Version, e.Detail, e.At)).ToList());
    }
}
