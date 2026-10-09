namespace Marketplace.Modules.Location.Contracts;

/// <summary>
/// Where an item is. Give a gazetteer <paramref name="PlaceId"/> (area centre) or exact coordinates.
/// <paramref name="Address"/> and exact coordinates stay private inside the Location module.
/// </summary>
public sealed record ItemLocationRequest(int? PlaceId, double? Latitude, double? Longitude, string Address, string? AreaName);

/// <summary>Public-grade view of an item location: a point fuzzed to a ~1 km cell and an area name. Safe to show.</summary>
public sealed record PublicItemLocation(Guid LocationId, double Latitude, double Longitude, string AreaName);

/// <summary>Location module API for other modules (system design §4).</summary>
public interface ILocationService
{
    Task<PublicItemLocation> CreateItemLocationAsync(Guid ownerId, ItemLocationRequest request, CancellationToken ct);

    /// <summary>
    /// The exact pickup address. Only Orders may call this, and only for the buyer (or seller) of a deal
    /// agreement both parties accepted (feature design §9.2).
    /// </summary>
    Task<string?> GetPickupAddressAsync(Guid locationId, CancellationToken ct);
}
