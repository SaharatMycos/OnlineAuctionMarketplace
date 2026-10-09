using Marketplace.Modules.Search.Domain;
using Marketplace.SharedKernel.Geo;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Modules.Search.Persistence;

/// <summary>Code-first model of the "search" schema.</summary>
internal sealed class SearchDbContext(DbContextOptions<SearchDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => SearchModule.Schema;

    public DbSet<ListingCard> ListingCards => Set<ListingCard>();
}

internal sealed class ListingCardConfiguration : IEntityTypeConfiguration<ListingCard>
{
    public void Configure(EntityTypeBuilder<ListingCard> e)
    {
        e.ToTable("listing_card");
        e.HasKey(c => c.ListingId);
        e.HasIndex(c => c.AuctionId);
        e.HasIndex(c => new { c.Status, c.EndsAt });
        e.HasIndex(c => c.PublicGeo).HasMethod("gist");
        e.Property(c => c.PublicGeo).HasColumnType(GeoPoints.ColumnType);
        e.Property(c => c.SellerHandle).HasMaxLength(30);
        e.Property(c => c.Title).HasMaxLength(80);
        e.Property(c => c.CategoryName).HasMaxLength(60);
        e.Property(c => c.Emoji).HasMaxLength(8);
        e.Property(c => c.Condition).HasMaxLength(40);
        e.Property(c => c.Delivery).HasMaxLength(10);
        e.Property(c => c.AreaName).HasMaxLength(100);
        e.Property(c => c.Currency).HasMaxLength(3);
        e.Property(c => c.Status).HasMaxLength(10);
        e.Property(c => c.Price).HasPrecision(12, 2);
        e.Property(c => c.BuyNowPrice).HasPrecision(12, 2);
        e.Property(c => c.ShippingCost).HasPrecision(12, 2);
    }
}
