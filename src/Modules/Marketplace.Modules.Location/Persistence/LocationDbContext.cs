using Marketplace.Modules.Location.Domain;
using Marketplace.SharedKernel.Geo;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Modules.Location.Persistence;

/// <summary>Code-first model of the "location" schema.</summary>
internal sealed class LocationDbContext(DbContextOptions<LocationDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => LocationModule.Schema;

    public DbSet<Place> Places => Set<Place>();
    public DbSet<ItemLocation> ItemLocations => Set<ItemLocation>();
    public DbSet<UserLocation> UserLocations => Set<UserLocation>();
}

internal sealed class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> e)
    {
        e.ToTable("place");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).ValueGeneratedNever();
        e.Property(p => p.Name).HasMaxLength(100);
        e.Property(p => p.City).HasMaxLength(100);
        e.Property(p => p.Kind).HasMaxLength(10);
        e.HasData(Gazetteer.Places);
    }
}

internal sealed class ItemLocationConfiguration : IEntityTypeConfiguration<ItemLocation>
{
    public void Configure(EntityTypeBuilder<ItemLocation> e)
    {
        e.ToTable("item_location");
        e.HasKey(l => l.Id);
        e.Property(l => l.Address).HasMaxLength(300);
        e.Property(l => l.Geo).HasColumnType(GeoPoints.ColumnType);
        e.Property(l => l.PublicGeo).HasColumnType(GeoPoints.ColumnType);
        e.Property(l => l.AreaName).HasMaxLength(100);
        e.HasIndex(l => l.OwnerId);
    }
}

internal sealed class UserLocationConfiguration : IEntityTypeConfiguration<UserLocation>
{
    public void Configure(EntityTypeBuilder<UserLocation> e)
    {
        e.ToTable("user_location");
        e.HasKey(l => l.UserId);
        e.Property(l => l.PublicGeo).HasColumnType(GeoPoints.ColumnType);
        e.Property(l => l.AreaName).HasMaxLength(100);
        e.Property(l => l.Source).HasMaxLength(10);
    }
}

/// <summary>Demo gazetteer: the mockup's cities plus Chicago-area neighbourhoods (the demo region).</summary>
internal static class Gazetteer
{
    public static readonly Place[] Places =
    [
        new() { Id = 1, Name = "Chicago, IL", City = "Chicago", Latitude = 41.8781, Longitude = -87.6298, Kind = "city" },
        new() { Id = 2, Name = "Brooklyn, NY", City = "New York", Latitude = 40.6782, Longitude = -73.9442, Kind = "city" },
        new() { Id = 3, Name = "Dallas, TX", City = "Dallas", Latitude = 32.7767, Longitude = -96.7970, Kind = "city" },
        new() { Id = 4, Name = "Seattle, WA", City = "Seattle", Latitude = 47.6062, Longitude = -122.3321, Kind = "city" },
        new() { Id = 5, Name = "Portland, OR", City = "Portland", Latitude = 45.5152, Longitude = -122.6784, Kind = "city" },
        new() { Id = 6, Name = "San Diego, CA", City = "San Diego", Latitude = 32.7157, Longitude = -117.1611, Kind = "city" },
        new() { Id = 101, Name = "Wicker Park, Chicago", City = "Chicago", Latitude = 41.9088, Longitude = -87.6776, Kind = "area" },
        new() { Id = 102, Name = "Oak Park", City = "Chicago", Latitude = 41.8850, Longitude = -87.7845, Kind = "area" },
        new() { Id = 103, Name = "Evanston", City = "Chicago", Latitude = 42.0451, Longitude = -87.6877, Kind = "area" },
        new() { Id = 104, Name = "Naperville", City = "Chicago", Latitude = 41.7508, Longitude = -88.1535, Kind = "area" },
        new() { Id = 105, Name = "West Loop, Chicago", City = "Chicago", Latitude = 41.8827, Longitude = -87.6480, Kind = "area" },
        new() { Id = 106, Name = "Hyde Park, Chicago", City = "Chicago", Latitude = 41.7943, Longitude = -87.5907, Kind = "area" },
        new() { Id = 107, Name = "Lincoln Park, Chicago", City = "Chicago", Latitude = 41.9214, Longitude = -87.6513, Kind = "area" },
        new() { Id = 108, Name = "The Loop, Chicago", City = "Chicago", Latitude = 41.8786, Longitude = -87.6251, Kind = "area" },
    ];
}
