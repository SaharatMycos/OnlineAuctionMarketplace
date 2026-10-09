using System.Text.Json;

namespace Marketplace.SharedKernel.Events;

/// <summary>
/// An event one module publishes for others (system design §2). Declare it as a record in the
/// publishing module's <c>*.Contracts</c> project and give it a stable, versioned name.
/// </summary>
public interface IIntegrationEvent;

/// <summary>Stable wire name, e.g. <c>auctions.auction_closed.v1</c>. Never reuse a name for a different shape.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>An event that should also be pushed to browsers through the realtime process.</summary>
public interface IRealtimeEvent
{
    IEnumerable<RealtimePush> RealtimePushes();
}

/// <param name="Group">SignalR group, e.g. <c>auction:{id}</c> or <c>user:{id}</c>.</param>
/// <param name="Event">Client event name, e.g. <c>bid.placed</c>.</param>
/// <param name="Payload">Serialized as JSON for the client. Must never contain private data (proxy maxima, exact locations).</param>
public sealed record RealtimePush(string Group, string Event, object Payload);

public static class RealtimeGroups
{
    public static string Auction(Guid auctionId) => $"auction:{auctionId}";
    public static string User(Guid userId) => $"user:{userId}";
}

public static class EventJson
{
    /// <summary>Same conventions as the HTTP API: camelCase names, enums as camelCase strings.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/// <summary>Public bidder identities are masked (feature design §4.5): <c>alice</c> → <c>a***e</c>.</summary>
public static class Handles
{
    public static string Mask(string handle) => handle.Length switch
    {
        0 => "***",
        1 or 2 => handle[0] + "***",
        _ => $"{handle[0]}***{handle[^1]}",
    };
}
