namespace Marketplace.Modules.Catalog.Domain;

internal sealed class ListingPhoto
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SellerId { get; init; }
    public Guid? ListingId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UnattachedAt { get; set; }
    public required string ContentType { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public required byte[] Content { get; init; }
    private const string UrlPrefix = "/v1/listing-photos/";

    // API-relative, so each client resolves it against its own API base (the web adds its BFF prefix).
    public static string Url(Guid id) => UrlPrefix + id.ToString("D");

    public static bool TryParseUrl(string value, out Guid id)
    {
        id = default;
        return value.StartsWith(UrlPrefix, StringComparison.Ordinal) && Guid.TryParseExact(value[UrlPrefix.Length..], "D", out id);
    }
}
