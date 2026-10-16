using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.SharedKernel.Jobs;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Marketplace.IntegrationTests;

[Collection(MarketplaceCollection.Name)]
public sealed class ListingPhotoTests(MarketplaceFixture app)
{
    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(24, 16);
        bitmap.Erase(SKColors.Blue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[]? bytes = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes ?? Png());
        // Intentionally inaccurate: the processor validates bytes rather than trusting MIME.
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "photo.png");
        return await client.PostAsync("/v1/listing-photos", form);
    }

    private static async Task<Guid> UploadId(HttpClient client) =>
        (await (await Upload(client)).ReadAsync(HttpStatusCode.Created)).GetProperty("id").GetGuid();

    private static object Input(Guid[]? photoIds = null, string[]? photoUrls = null, string title = "Uploaded photo listing") => new
    {
        categoryId = 9, title, description = "A listing with uploaded photos.", condition = "Used",
        delivery = "pickup", startPrice = 10, durationMinutes = 60, photoIds, photoUrls,
    };

    private static Task<HttpResponseMessage> Create(HttpClient client, Guid[]? ids = null, bool publish = false, string[]? urls = null) =>
        client.PostAsJsonAsync("/v1/listings", new { listing = Input(ids, urls), location = new { placeId = 1, address = Scenario.SecretAddress }, publish });

    [Fact]
    public async Task Upload_requires_auth_and_private_photos_are_not_public()
    {
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(anonymous)).StatusCode);
        using var seller = await app.SignInAsync("photo-private");
        using var other = await app.SignInAsync("photo-other");
        var id = await UploadId(seller);
        var url = $"/v1/listing-photos/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        using var response = await seller.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xff, 0xd8 }, bytes[..2]);
        using var decoded = SKBitmap.Decode(bytes);
        Assert.Equal((24, 16), (decoded.Width, decoded.Height));
        Assert.Equal(bytes, await seller.GetByteArrayAsync(url));
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await seller.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Draft_photos_become_public_in_order_and_cover_flows_to_search()
    {
        using var seller = await app.SignInAsync("photo-publisher");
        using var anonymous = app.CreateClient();
        var first = await UploadId(seller);
        var second = await UploadId(seller);
        var draft = await (await Create(seller, [first, second])).ReadAsync(HttpStatusCode.Created);
        var listing = draft.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/v1/listing-photos/{first}")).StatusCode);
        var updated = await (await seller.PutAsJsonAsync($"/v1/listings/{listing}", Input([second, first]))).ReadAsync();
        Assert.Equal($"/v1/listing-photos/{second}", updated.GetProperty("photoUrls")[0].GetString());
        await (await seller.PostAsync($"/v1/listings/{listing}/publish", null)).ReadAsync();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/v1/listing-photos/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.DeleteAsync($"/v1/listing-photos/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await seller.PutAsJsonAsync($"/v1/listings/{listing}", Input([first]))).StatusCode);
        await app.DrainOutboxAsync();
        var cards = await (await anonymous.GetAsync("/v1/search?q=Uploaded%20photo%20listing")).ReadAsync();
        var card = cards.EnumerateArray().Single(c => c.GetProperty("listingId").GetGuid() == listing);
        Assert.Equal($"/v1/listing-photos/{second}", card.GetProperty("thumbnailUrl").GetString());
    }

    [Fact]
    public async Task Draft_update_round_trips_or_omits_photos_and_public_photos_are_cacheable()
    {
        using var seller = await app.SignInAsync("photo-roundtrip");
        using var anonymous = app.CreateClient();
        var id = await UploadId(seller);
        var listing = (await (await Create(seller, [id])).ReadAsync(HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var url = $"/v1/listing-photos/{id}";
        var roundTripped = await (await seller.PutAsJsonAsync($"/v1/listings/{listing}", Input(photoUrls: [url]))).ReadAsync();
        Assert.Equal(url, roundTripped.GetProperty("photoUrls")[0].GetString());
        var omitted = await (await seller.PutAsJsonAsync($"/v1/listings/{listing}", Input(title: "Renamed photo listing"))).ReadAsync();
        Assert.Equal(url, omitted.GetProperty("photoUrls")[0].GetString());
        await (await seller.PostAsync($"/v1/listings/{listing}/publish", null)).ReadAsync();
        using var response = await anonymous.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.Public);
    }

    [Fact]
    public async Task Ownership_duplicates_missing_ids_mixed_inputs_and_count_are_checked()
    {
        using var seller = await app.SignInAsync("photo-validation");
        using var other = await app.SignInAsync("photo-validation-other");
        var id = await UploadId(seller);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(other, [id])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(seller, [id, id])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(seller, [Guid.NewGuid()])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(seller, Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(seller, [id], urls: ["https://example.test/photo.jpg"])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(seller, urls: [$"/v1/listing-photos/{id}"])).StatusCode);
        // Failed requests leave uploads available for retry.
        await (await Create(seller, [id])).ReadAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Concurrent_attachment_allows_only_one_listing()
    {
        using var seller = await app.SignInAsync("photo-race");
        var id = await UploadId(seller);
        var responses = await Task.WhenAll(Create(seller, [id]), Create(seller, [id]));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cleanup_only_removes_expired_unattached_photos()
    {
        using var seller = await app.SignInAsync("photo-cleanup");
        var abandoned = await UploadId(seller);
        var attached = await UploadId(seller);
        var detached = await UploadId(seller);
        var listing = (await (await Create(seller, [attached, detached])).ReadAsync(HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var job = app.Services.GetServices<IBackgroundJob>().Single(j => j.Name == "catalog.expire_photos");
        app.Clock.Advance(TimeSpan.FromHours(25));
        await (await seller.PutAsJsonAsync($"/v1/listings/{listing}", Input([attached]))).ReadAsync();
        var fresh = await UploadId(seller);
        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync($"/v1/listing-photos/{abandoned}")).StatusCode);
        foreach (var id in new[] { attached, detached, fresh }) Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync($"/v1/listing-photos/{id}")).StatusCode);
        Assert.Equal(0, await job.RunOnceAsync(CancellationToken.None));
        app.Clock.Advance(TimeSpan.FromHours(25));
        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync($"/v1/listing-photos/{detached}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync($"/v1/listing-photos/{attached}")).StatusCode);
    }

    [Fact]
    public async Task Legacy_external_urls_and_optional_photos_still_work()
    {
        using var seller = await app.SignInAsync("photo-legacy");
        var legacy = await (await Create(seller, publish: true, urls: ["https://example.test/cover.jpg"])).ReadAsync(HttpStatusCode.Created);
        Assert.Equal("https://example.test/cover.jpg", legacy.GetProperty("photoUrls")[0].GetString());
        var noPhotos = await (await Create(seller)).ReadAsync(HttpStatusCode.Created);
        Assert.Equal(0, noPhotos.GetProperty("photoUrls").GetArrayLength());
    }

    [Fact]
    public async Task Invalid_empty_and_oversized_uploads_are_rejected()
    {
        using var seller = await app.SignInAsync("photo-invalid");
        foreach (var bytes in new byte[][] { [], [1, 2, 3] })
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(seller, bytes)).StatusCode);
        var response = await Upload(seller, new byte[10 * 1024 * 1024 + 64 * 1024 + 1]);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
