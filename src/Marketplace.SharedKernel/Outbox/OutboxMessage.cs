using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.SharedKernel.Outbox;

/// <summary>
/// An integration event waiting to be relayed by the worker (system design §2). Each module has its
/// own <c>outbox_message</c> table in its own schema, so the event commits atomically with the change.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Type { get; init; }
    /// <summary>Event body as JSON (stored as jsonb).</summary>
    public required string Payload { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> e)
    {
        e.ToTable("outbox_message");
        e.HasKey(m => m.Id);
        e.Property(m => m.Id).ValueGeneratedNever();
        e.Property(m => m.Type).HasMaxLength(200);
        e.Property(m => m.Payload).HasColumnType("jsonb");
        e.HasIndex(m => m.OccurredAt)
            .HasDatabaseName("ix_outbox_message_pending")
            .HasFilter("processed_at IS NULL");
    }
}
