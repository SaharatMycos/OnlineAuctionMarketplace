using Marketplace.SharedKernel.Events;

namespace Marketplace.Modules.Orders.Contracts;

/// <summary>
/// Anything happened to an order or its deal agreement (created, terms changed, accepted, paid, received,
/// cancelled). Both parties get a realtime nudge to refresh; details are fetched through the API, which
/// enforces who can see what.
/// </summary>
[IntegrationEvent("orders.order_updated.v1")]
public sealed record OrderUpdated(
    Guid OrderId,
    Guid ListingId,
    string AgreementNumber,
    string Title,
    Guid BuyerId,
    Guid SellerId,
    string Status,
    int Version,
    string Action,
    Guid? ActorId) : IIntegrationEvent, IRealtimeEvent
{
    public IEnumerable<RealtimePush> RealtimePushes()
    {
        var payload = new { OrderId, ListingId, AgreementNumber, Title, Status, Version, Action };
        yield return new RealtimePush(RealtimeGroups.User(BuyerId), "order.updated", payload);
        yield return new RealtimePush(RealtimeGroups.User(SellerId), "order.updated", payload);
    }
}
