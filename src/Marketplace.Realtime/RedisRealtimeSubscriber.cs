using System.Text.Json;
using Marketplace.Bootstrap;
using Marketplace.SharedKernel.Events;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Marketplace.Realtime;

/// <summary>Receives pushes the worker relayed to Redis and forwards them to SignalR groups.</summary>
public sealed class RedisRealtimeSubscriber(
    IConnectionMultiplexer redis,
    IHubContext<AuctionHub> hub,
    ILogger<RedisRealtimeSubscriber> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = await redis.GetSubscriber().SubscribeAsync(RealtimeChannel.Name);
        logger.LogInformation("Subscribed to {Channel}", RealtimeChannel.Name);

        await foreach (var message in queue.WithCancellation(stoppingToken))
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<RealtimeEnvelope>((string)message.Message!, EventJson.Options)!;
                await hub.Clients.Group(envelope.Group).SendAsync(envelope.Event, envelope.Payload, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Dropped a malformed realtime message");
            }
        }
    }
}
