using Marketplace.Modules.Auctions.Domain;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Modules.Auctions.Persistence;

/// <summary>Code-first model of the "auctions" schema.</summary>
internal sealed class AuctionsDbContext(DbContextOptions<AuctionsDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => AuctionsModule.Schema;

    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<BidRequest> BidRequests => Set<BidRequest>();

    /// <summary>Loads the auction with a row lock (<c>SELECT … FOR UPDATE</c>): the single writer per auction.</summary>
    public async Task<Auction?> LockAuctionAsync(Guid auctionId, CancellationToken ct) =>
        (await Auctions.FromSql($"SELECT * FROM auctions.auction WHERE id = {auctionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
}

/// <summary>Remembers the response to each (bidder, Idempotency-Key) so retries never place a second bid.</summary>
internal sealed class BidRequest
{
    public Guid BidderId { get; init; }
    public required string IdempotencyKey { get; init; }
    public Guid AuctionId { get; init; }
    public required string Response { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

internal sealed class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> e)
    {
        e.ToTable("auction");
        e.HasKey(a => a.Id);
        e.HasIndex(a => a.ListingId).IsUnique();
        e.HasIndex(a => new { a.Status, a.EndsAt });
        e.Property(a => a.SellerHandle).HasMaxLength(30);
        e.Property(a => a.LeaderHandle).HasMaxLength(30);
        e.Property(a => a.Title).HasMaxLength(80);
        e.Property(a => a.Currency).HasMaxLength(3);
        e.Property(a => a.CloseReason).HasMaxLength(20);
        e.Property(a => a.Status).HasConversion<string>().HasMaxLength(15);
        foreach (var money in new[] { nameof(Auction.StartPrice), nameof(Auction.ReservePrice), nameof(Auction.BuyNowPrice),
                     nameof(Auction.CurrentPrice), nameof(Auction.LeaderMax), nameof(Auction.FinalPrice) })
            e.Property(money).HasPrecision(12, 2);
    }
}

internal sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> e)
    {
        e.ToTable("bid");
        e.HasKey(b => b.Id);
        e.HasIndex(b => new { b.AuctionId, b.Seq }).IsUnique();
        e.HasIndex(b => new { b.BidderId, b.AuctionId });
        e.Property(b => b.BidderHandle).HasMaxLength(30);
        e.Property(b => b.Amount).HasPrecision(12, 2);
        e.Property(b => b.MaxAmount).HasPrecision(12, 2);
        e.Property(b => b.Kind).HasConversion<string>().HasMaxLength(10);
        e.HasOne<Auction>().WithMany().HasForeignKey(b => b.AuctionId);
    }
}

internal sealed class BidRequestConfiguration : IEntityTypeConfiguration<BidRequest>
{
    public void Configure(EntityTypeBuilder<BidRequest> e)
    {
        e.ToTable("bid_request");
        e.HasKey(r => new { r.BidderId, r.IdempotencyKey });
        e.Property(r => r.IdempotencyKey).HasMaxLength(100);
        e.Property(r => r.Response).HasColumnType("jsonb");
    }
}
