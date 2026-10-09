using System.Text.Json;
using Marketplace.Modules.Catalog.Domain;
using Marketplace.SharedKernel.Geo;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Modules.Catalog.Persistence;

/// <summary>Code-first model of the "catalog" schema.</summary>
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => CatalogModule.Schema;

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Listing> Listings => Set<Listing>();
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e)
    {
        e.ToTable("category");
        e.HasKey(c => c.Id);
        e.Property(c => c.Id).ValueGeneratedNever();
        e.Property(c => c.Name).HasMaxLength(60);
        e.Property(c => c.Slug).HasMaxLength(60);
        e.Property(c => c.Emoji).HasMaxLength(8);
        e.HasIndex(c => c.Slug).IsUnique();
        e.HasData(
            new Category { Id = 1, Name = "Electronics", Slug = "electronics", Emoji = "📷" },
            new Category { Id = 2, Name = "Collectibles", Slug = "collectibles", Emoji = "🃏" },
            new Category { Id = 3, Name = "Fashion", Slug = "fashion", Emoji = "👟" },
            new Category { Id = 4, Name = "Home & Garden", Slug = "home-garden", Emoji = "🛋️" },
            new Category { Id = 5, Name = "Toys & Hobbies", Slug = "toys-hobbies", Emoji = "🧱" },
            new Category { Id = 6, Name = "Industrial", Slug = "industrial", Emoji = "🏭" },
            new Category { Id = 7, Name = "Liquidation lots", Slug = "liquidation-lots", Emoji = "📦" },
            new Category { Id = 8, Name = "Food & Agriculture", Slug = "food-agriculture", Emoji = "🌹" },
            new Category { Id = 9, Name = "Sports & Outdoors", Slug = "sports-outdoors", Emoji = "🚲" });
    }
}

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> e)
    {
        e.ToTable("listing");
        e.HasKey(l => l.Id);
        e.HasOne(l => l.Category).WithMany().HasForeignKey(l => l.CategoryId);
        e.Property(l => l.SellerHandle).HasMaxLength(30);
        e.Property(l => l.Title).HasMaxLength(80);
        e.Property(l => l.Description).HasMaxLength(4000);
        e.Property(l => l.Condition).HasMaxLength(40);
        e.Property(l => l.Specs)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
                v => new Dictionary<string, string>(v)));
        e.Property(l => l.Delivery).HasConversion<string>().HasMaxLength(10);
        e.Property(l => l.Status).HasConversion<string>().HasMaxLength(10);
        e.Property(l => l.PublicGeo).HasColumnType(GeoPoints.ColumnType);
        e.Property(l => l.AreaName).HasMaxLength(100);
        e.Property(l => l.Currency).HasMaxLength(3);
        e.Property(l => l.StartPrice).HasPrecision(12, 2);
        e.Property(l => l.ReservePrice).HasPrecision(12, 2);
        e.Property(l => l.BuyNowPrice).HasPrecision(12, 2);
        e.Property(l => l.ShippingCost).HasPrecision(12, 2);
        e.HasIndex(l => new { l.SellerId, l.CreatedAt });
    }
}
