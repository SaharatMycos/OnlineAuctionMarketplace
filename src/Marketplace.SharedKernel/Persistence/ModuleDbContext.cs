using System.Text.Json;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.SharedKernel.Persistence;

/// <summary>
/// Base class for every module's DbContext (EF Core code-first). The context owns exactly one
/// Postgres schema (<see cref="Schema"/> = <see cref="IModule.Name"/>): all its tables, its outbox,
/// its inbox and its migrations history live there. Entity configurations in the module's assembly
/// are applied automatically.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public abstract string Schema { get; }

    /// <summary>The module's transactional outbox. Use <see cref="Publish{TEvent}"/> to add to it.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Messages this module's handlers have already processed.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Queues an integration event; it commits with the next SaveChanges, atomically with the state change.</summary>
    public void Publish<TEvent>(TEvent @event, DateTimeOffset occurredAt) where TEvent : IIntegrationEvent =>
        OutboxMessages.Add(new OutboxMessage
        {
            Type = IntegrationEventRegistry.NameOf(typeof(TEvent)),
            Payload = JsonSerializer.Serialize(@event, EventJson.Options),
            OccurredAt = occurredAt,
        });

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
