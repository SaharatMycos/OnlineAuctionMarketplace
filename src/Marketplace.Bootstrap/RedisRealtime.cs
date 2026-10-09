using System.Text.Json;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Marketplace.Bootstrap;

/// <summary>Wire format on the Redis channel between the worker (publisher) and the realtime process (subscriber).</summary>
public sealed record RealtimeEnvelope(string Group, string Event, JsonElement Payload);

public static class RealtimeChannel
{
    public static readonly RedisChannel Name = RedisChannel.Literal("marketplace:realtime");
}

internal sealed class RedisRealtimePublisher(IConnectionMultiplexer redis) : IRealtimePublisher
{
    public Task PublishAsync(RealtimePush push, CancellationToken ct)
    {
        var envelope = new RealtimeEnvelope(push.Group, push.Event, JsonSerializer.SerializeToElement(push.Payload, EventJson.Options));
        return redis.GetSubscriber().PublishAsync(RealtimeChannel.Name, JsonSerializer.Serialize(envelope, EventJson.Options));
    }
}

public static class RedisServiceCollectionExtensions
{
    /// <summary>Shared Redis connection from ConnectionStrings:Redis (resolved lazily).</summary>
    public static IServiceCollection AddRedis(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");
            return ConnectionMultiplexer.Connect(connectionString);
        });
        return services;
    }

    /// <summary>Worker: send realtime pushes to Redis instead of dropping them.</summary>
    public static IServiceCollection AddRedisRealtimePublisher(this IServiceCollection services)
    {
        services.AddRedis();
        services.Replace(ServiceDescriptor.Singleton<IRealtimePublisher, RedisRealtimePublisher>());
        return services;
    }
}
