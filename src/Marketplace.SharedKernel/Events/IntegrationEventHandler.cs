using Marketplace.SharedKernel.Outbox;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.SharedKernel.Events;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(Guid messageId, TEvent @event, CancellationToken ct);
}

/// <summary>
/// Base for handlers that change a module's own data. The work, any events it publishes and the
/// inbox row commit in one transaction, so a redelivered message (at-least-once relay) has no effect.
/// </summary>
public abstract class IntegrationEventHandler<TEvent, TContext>(TContext db, IClock clock) : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
    where TContext : ModuleDbContext
{
    protected TContext Db { get; } = db;
    protected IClock Clock { get; } = clock;

    public async Task HandleAsync(Guid messageId, TEvent @event, CancellationToken ct)
    {
        var handler = GetType().FullName!;
        if (await Db.InboxMessages.AnyAsync(m => m.MessageId == messageId && m.Handler == handler, ct))
            return;

        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        await HandleAsync(@event, ct);
        Db.InboxMessages.Add(new InboxMessage { MessageId = messageId, Handler = handler, ProcessedAt = Clock.UtcNow });
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Change <see cref="Db"/> (and publish follow-up events); don't call SaveChanges.</summary>
    protected abstract Task HandleAsync(TEvent @event, CancellationToken ct);
}

/// <summary>Links an event type to a handler type; the outbox processor runs each handler in its own scope.</summary>
public sealed record HandlerRegistration(Type EventType, Type HandlerType);

public static class IntegrationEventServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new HandlerRegistration(typeof(TEvent), typeof(THandler)));
        return services;
    }
}
