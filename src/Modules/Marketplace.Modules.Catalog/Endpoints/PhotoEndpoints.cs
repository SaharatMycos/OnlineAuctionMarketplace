using Marketplace.Media;
using Marketplace.Modules.Catalog.Domain;
using Marketplace.Modules.Catalog.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Catalog.Endpoints;

internal static class PhotoEndpoints
{
    internal sealed record PhotoResponse(Guid Id, string Url, int Width, int Height);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/listing-photos").WithTags("Catalog");
        // Read the form manually: browser requests are authenticated by the BFF's bearer token,
        // not by ambient API cookies. No IFormFile binding / antiforgery middleware is required.
        group.MapPost("", Upload).RequireAuthorization();
        group.MapGet("/{id:guid}", Get);
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization();
    }

    private static async Task<IResult> Upload(HttpContext http, ICurrentUser current, IImageProcessor processor,
        CatalogDbContext db, IClock clock, CancellationToken ct)
    {
        var seller = current.RequireId();
        var limit = ImageLimits.MaxBytes + 64 * 1024;
        var bodyFeature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyFeature is { IsReadOnly: false }) bodyFeature.MaxRequestBodySize = limit;
        if (http.Request.ContentLength > limit) return Results.Problem(statusCode: 413, detail: "Photos must be at most 10 MiB.");
        if (!http.Request.HasFormContentType)
            throw ProblemException.BadRequest("invalid_photo", "Send one multipart photo file.");
        IFormCollection form;
        try { form = await http.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = ImageLimits.MaxBytes, BufferBodyLengthLimit = limit }, ct); }
        catch (InvalidDataException) { throw ProblemException.BadRequest("invalid_photo", "Invalid or oversized multipart upload."); }
        if (form.Files.Count != 1 || form.Files[0].Length == 0)
            throw ProblemException.BadRequest("invalid_photo", "Send exactly one nonempty photo file.");
        try
        {
            await using var stream = form.Files[0].OpenReadStream();
            var image = await processor.ProcessAsync(stream, ct);
            var photo = new ListingPhoto
            {
                SellerId = seller, CreatedAt = clock.UtcNow, UnattachedAt = clock.UtcNow,
                Content = image.Content, ContentType = image.ContentType, Width = image.Width, Height = image.Height,
            };
            db.ListingPhotos.Add(photo);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/v1/listing-photos/{photo.Id}", new PhotoResponse(photo.Id, ListingPhoto.Url(photo.Id), photo.Width, photo.Height));
        }
        catch (ImageProcessingException e)
        {
            throw ProblemException.BadRequest("invalid_photo", e.Message);
        }
    }

    private static async Task<IResult> Get(Guid id, HttpContext http, CatalogDbContext db, ICurrentUser current, CancellationToken ct)
    {
        // Apply visibility in SQL before loading potentially large image bytes.
        var photo = await db.ListingPhotos.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.SellerId, p.Content, p.ContentType, IsPublic = db.Listings.Any(l => l.Id == p.ListingId && l.Status != ListingStatus.Draft) })
            .Where(p => p.SellerId == current.Id || p.IsPublic).SingleOrDefaultAsync(ct)
            ?? throw ProblemException.NotFound("Photo");
        // Bytes never change for an ID; public copies may be cached, owner-only drafts may not.
        http.Response.Headers.CacheControl = photo.IsPublic ? "public, max-age=86400" : "private, no-store";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Bytes(photo.Content, photo.ContentType);
    }

    private static async Task<IResult> Delete(Guid id, CatalogDbContext db, ICurrentUser current, CancellationToken ct)
    {
        // Atomic predicate prevents deletion after a concurrent attachment.
        var deleted = await db.ListingPhotos.Where(p => p.Id == id && p.SellerId == current.RequireId() && p.ListingId == null).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw ProblemException.NotFound("Unattached photo");
        return Results.NoContent();
    }
}
