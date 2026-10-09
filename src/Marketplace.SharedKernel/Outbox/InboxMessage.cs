using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.SharedKernel.Outbox;

/// <summary>Records that a handler in this module already processed a message (idempotent consumers).</summary>
public sealed class InboxMessage
{
    public required Guid MessageId { get; init; }
    public required string Handler { get; init; }
    public required DateTimeOffset ProcessedAt { get; init; }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> e)
    {
        e.ToTable("inbox_message");
        e.HasKey(m => new { m.MessageId, m.Handler });
        e.Property(m => m.Handler).HasMaxLength(300);
    }
}
