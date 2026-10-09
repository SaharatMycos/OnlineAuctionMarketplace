using NetTopologySuite.Geometries;

namespace Marketplace.Modules.Location.Domain;

/// <summary>Gazetteer entry used for city/area search until a geocoding vendor is plugged in (system design §4).</summary>
internal sealed class Place
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string City { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    /// <summary><c>city</c> for the location picker, <c>area</c> for neighbourhoods.</summary>
    public required string Kind { get; init; }
}

/// <summary>
/// Where an item is. <see cref="Address"/> and <see cref="Geo"/> are private: only the deal agreement
/// reveals the address, to the buyer, after both parties accept. Everything public uses <see cref="PublicGeo"/>.
/// </summary>
internal sealed class ItemLocation
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid OwnerId { get; init; }
    public required string Address { get; init; }
    public required Point Geo { get; init; }
    public required Point PublicGeo { get; init; }
    public required string AreaName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>A signed-in buyer's search location: public-grade point only, plus their radius (null = anywhere).</summary>
internal sealed class UserLocation
{
    public Guid UserId { get; init; }
    public required Point PublicGeo { get; set; }
    public required string AreaName { get; set; }
    public int? RadiusKm { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static readonly int[] AllowedRadiiKm = [5, 10, 25, 50, 100];
    public const int DefaultRadiusKm = 25;
}
