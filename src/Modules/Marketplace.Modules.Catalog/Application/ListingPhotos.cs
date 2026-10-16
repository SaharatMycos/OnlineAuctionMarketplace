using Marketplace.Modules.Catalog.Domain;
using Marketplace.Modules.Catalog.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Catalog.Application;

internal static class ListingPhotos
{
    // Caller holds a listing lock (updates) and a transaction until SaveChanges commits.
    public static async Task ApplyAsync(CatalogDbContext db, Listing listing, List<Guid>? ids, List<string>? urls,
        DateTimeOffset now, CancellationToken ct)
    {
        // Omitting both photo fields keeps the current photos.
        if (ids is null && urls is null) return;
        // Round-tripped responses carry the listing's own photo URLs; accept those as IDs of already-attached photos.
        var roundTripped = ids is null && urls!.Any(u => ListingPhoto.TryParseUrl(u.Trim(), out _));
        if (roundTripped)
        {
            ids = urls!.Select(u => ListingPhoto.TryParseUrl(u.Trim(), out var id) ? id : (Guid?)null).OfType<Guid>().ToList();
            urls = urls!.Where(u => !ListingPhoto.TryParseUrl(u.Trim(), out _)).ToList();
        }
        ids ??= [];
        if (ids.Count > 24 || ids.Distinct().Count() != ids.Count || (ids.Count > 0 && urls?.Any(u => !string.IsNullOrWhiteSpace(u)) == true))
            throw ProblemException.BadRequest("invalid_photos", "Use up to 24 distinct photo IDs, or external URLs, but not both.");
        // Only this path may create local media URLs; clients must prove ownership with IDs.
        if (urls?.Any(u => !string.IsNullOrWhiteSpace(u) && (!Uri.TryCreate(u.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))) == true)
            throw ProblemException.BadRequest("invalid_photos", "External photo URLs must use http or https.");
        // Deterministic lock order prevents deadlocks for requests sharing several upload IDs.
        // Project the locked rows so image bytes are neither transferred nor tracked.
        var rows = await db.ListingPhotos.FromSql($"SELECT * FROM catalog.listing_photo WHERE id = ANY ({ids.ToArray()}) OR listing_id = {listing.Id} ORDER BY id FOR UPDATE")
            .Select(p => new { p.Id, p.SellerId, p.ListingId }).ToListAsync(ct);
        var selected = rows.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
        if (selected.Count != ids.Count || selected.Values.Any(p => p.SellerId != listing.SellerId ||
                (roundTripped ? p.ListingId != listing.Id : p.ListingId != null && p.ListingId != listing.Id)))
            throw ProblemException.BadRequest("invalid_photos", "A photo is unavailable or does not belong to you.");
        foreach (var row in rows)
        {
            // Stub entities: SaveChanges writes only the columns changed below, in FK order with a new listing.
            var photo = new ListingPhoto { Id = row.Id, SellerId = row.SellerId, ListingId = row.ListingId, ContentType = "", Content = [] };
            db.ListingPhotos.Attach(photo);
            if (selected.ContainsKey(photo.Id)) photo.ListingId = listing.Id;
            else { photo.ListingId = null; photo.UnattachedAt = now; }
        }
        if (ids.Count > 0) listing.PhotoUrls = ids.Select(ListingPhoto.Url).ToList();
    }
}

internal sealed class ExpireListingPhotos(IServiceScopeFactory scopes, IClock clock) : IBackgroundJob
{
    public string Name => "catalog.expire_photos";
    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var cutoff = clock.UtcNow.AddHours(-24);
        // The single statement locks a bounded batch; attachment races recheck listing_id.
        return await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM catalog.listing_photo WHERE id IN (SELECT id FROM catalog.listing_photo WHERE listing_id IS NULL AND unattached_at < {cutoff} ORDER BY id LIMIT 100 FOR UPDATE SKIP LOCKED) AND listing_id IS NULL", ct);
    }
}
