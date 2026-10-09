using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Marketplace.IntegrationTests;

/// <summary>Small API helpers shared by the integration tests.</summary>
public static class Scenario
{
    /// <summary>Exact coordinates and address that must never appear in public responses.</summary>
    public const double SecretLat = 41.909123, SecretLng = -87.677456;
    public const string SecretAddress = "1847 N Damen Ave, Apt 3R";

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task<string> ProblemCodeAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var problem = await response.ReadAsync(expected);
        return problem.GetProperty("code").GetString()!;
    }

    /// <summary>Lists and publishes an item, relays events, and returns (listingId, auctionId).</summary>
    public static async Task<(Guid ListingId, Guid AuctionId)> PublishAsync(
        this MarketplaceFixture app, HttpClient seller,
        string title = "Trek FX 3 Disc hybrid bike, size M",
        decimal startPrice = 150, decimal? reserve = null, decimal? buyNow = null,
        string delivery = "pickup", int durationMinutes = 60,
        double lat = SecretLat, double lng = SecretLng, string address = SecretAddress, int? placeId = null)
    {
        var response = await seller.PostAsJsonAsync("/v1/listings", new
        {
            listing = new
            {
                categoryId = 9,
                title,
                description = "Carbon fork, hydraulic disc brakes.",
                condition = "Used, good",
                specs = new Dictionary<string, string> { ["Brand"] = "Trek" },
                delivery,
                shippingCost = delivery == "pickup" ? (decimal?)null : 15,
                startPrice,
                reservePrice = reserve,
                buyNowPrice = buyNow,
                durationMinutes,
            },
            location = placeId is null
                ? (object)new { lat, lng, address }
                : new { placeId, address },
            publish = true,
        }, MarketplaceFixture.Json);
        var listing = await response.ReadAsync(HttpStatusCode.Created);
        var listingId = listing.GetProperty("id").GetGuid();

        await app.DrainOutboxAsync();
        var auction = await (await seller.GetAsync($"/v1/auctions/by-listing/{listingId}")).ReadAsync();
        return (listingId, auction.GetProperty("id").GetGuid());
    }

    public static Task<HttpResponseMessage> BidAsync(this HttpClient bidder, Guid auctionId, decimal max, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/auctions/{auctionId}/bids")
        {
            Content = JsonContent.Create(new { maxAmount = max }),
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return bidder.SendAsync(request);
    }

    public static async Task<JsonElement> GetAuctionAsync(this HttpClient client, Guid auctionId) =>
        await (await client.GetAsync($"/v1/auctions/{auctionId}")).ReadAsync();

    /// <summary>Moves the clock past the auction's end and runs the close job plus the event relay.</summary>
    public static async Task EndAuctionAsync(this MarketplaceFixture app, HttpClient client, Guid auctionId)
    {
        var auction = await client.GetAuctionAsync(auctionId);
        app.Clock.Set(auction.GetProperty("endsAt").GetDateTimeOffset().AddSeconds(1));
        await app.CloseDueAuctionsAsync();
    }
}
