using System.Text.Json;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Persistence;
using Marketplace.SharedKernel.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Marketplace.SharedKernel.Outbox;

/// <summary>
/// Relays every module's pending outbox messages: runs each registered handler in its own scope
/// (handlers are idempotent through their inbox), pushes realtime events, then marks the message
/// processed. Delivery is at least once. The worker calls this in a loop; tests call it directly.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IntegrationEventRegistry registry,
    IEnumerable<HandlerRegistration> handlers,
    IRealtimePublisher realtime,
    IClock clock,
    ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 10;

    private readonly ILookup<Type, Type> _handlersByEvent = handlers.ToLookup(h => h.EventType, h => h.HandlerType);

    /// <summary>Processes one batch per module. Returns how many messages were handled.</summary>
    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        List<Type> contextTypes;
        await using (var probe = scopeFactory.CreateAsyncScope())
            contextTypes = probe.ServiceProvider.GetServices<ModuleDbContext>().Select(c => c.GetType()).ToList();

        var processed = 0;
        foreach (var contextType in contextTypes)
            processed += await ProcessModuleAsync(contextType, ct);
        return processed;
    }

    /// <summary>Runs until no module has pending messages (useful in tests: events can cause more events).</summary>
    public async Task DrainAsync(CancellationToken ct = default)
    {
        for (var round = 0; round < 20; round++)
            if (await ProcessPendingAsync(ct) == 0)
                return;
        throw new InvalidOperationException("The outbox didn't drain after 20 rounds; check for an event loop.");
    }

    private async Task<int> ProcessModuleAsync(Type contextType, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Schema is a code constant, never user input. SKIP LOCKED lets several relays share the work.
        var batch = await db.OutboxMessages
            .FromSqlRaw($"""
                SELECT * FROM "{db.Schema}".outbox_message
                WHERE processed_at IS NULL AND attempts < {MaxAttempts}
                ORDER BY occurred_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in batch)
        {
            try
            {
                await DispatchAsync(message, ct);
                message.ProcessedAt = clock.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.ToString()[..Math.Min(ex.ToString().Length, 4000)];
                logger.LogError(ex, "Outbox message {Id} ({Type}) from {Schema} failed (attempt {Attempt})",
                    message.Id, message.Type, db.Schema, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return batch.Count;
    }

    private async Task DispatchAsync(OutboxMessage message, CancellationToken ct)
    {
        var eventType = registry.Resolve(message.Type)
            ?? throw new InvalidOperationException($"Unknown integration event '{message.Type}'.");
        var @event = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType, EventJson.Options)!;

        // Handlers are usually internal to their module, so call them through the public interface.
        var handle = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType).GetMethod("HandleAsync")!;
        foreach (var handlerType in _handlersByEvent[eventType])
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService(handlerType);
            try
            {
                await (Task)handle.Invoke(handler, [message.Id, @event, ct])!;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex.InnerException);
            }
        }

        if (@event is IRealtimeEvent realtimeEvent)
            foreach (var push in realtimeEvent.RealtimePushes())
                await realtime.PublishAsync(push, ct);
    }
}
