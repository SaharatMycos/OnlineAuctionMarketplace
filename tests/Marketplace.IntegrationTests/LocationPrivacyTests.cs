using System.Globalization;
using System.Text.Json;

namespace Marketplace.IntegrationTests;

/// <summary>Location-first discovery and privacy (feature design §9, system design §10).</summary>
[Collection(MarketplaceCollection.Name)]
public class LocationPrivacyTests(MarketplaceFixture app)
{
    // Chicago Loop (buyer) and two sellers: Wicker Park (~5 km) and Naperville (~45 km).
    private const double BuyerLat = 41.8786, BuyerLng = -87.6251;

    [Fact]
    public async Task Public_responses_never_expose_the_exact_location_or_address()
    {
        var alice = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var (listingId, auctionId) = await app.PublishAsync(alice, title: "Privacy probe bike", delivery: "both");
        await (await bob.BidAsync(auctionId, 200)).ReadAsync();
        await app.DrainOutboxAsync();

        string[] publicUrls =
        [
            $"/v1/listings/{listingId}",
            $"/v1/auctions/{auctionId}",
            $"/v1/auctions/{auctionId}/bids",
            $"/v1/feed/nearby?lat={BuyerLat}&lng={BuyerLng}&radiusKm=25",
            $"/v1/search?q=Privacy&lat={BuyerLat}&lng={BuyerLng}&radiusKm=100",
        ];
        var secrets = new[]
        {
            Scenario.SecretAddress,
            Scenario.SecretLat.ToString(CultureInfo.InvariantCulture),
            Scenario.SecretLng.ToString(CultureInfo.InvariantCulture),
            "41.9091",   // even partially precise coordinates
            "\"leaderMax\"",
            "\"maxAmount\"",
        };

        foreach (var client in new[] { app.CreateClient(), bob })
            foreach (var url in publicUrls)
            {
                var body = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
                foreach (var secret in secrets)
                    Assert.DoesNotContain(secret, body);
            }

        // Distances are whole kilometres from the fuzzed point.
        var search = await (await bob.GetAsync($"/v1/search?q=Privacy&lat={BuyerLat}&lng={BuyerLng}&radiusKm=100")).ReadAsync();
        var hit = Assert.Single(search.EnumerateArray(), c => c.GetProperty("listingId").GetGuid() == listingId);
        Assert.Equal(JsonValueKind.Number, hit.GetProperty("distanceKm").ValueKind);
        Assert.True(hit.GetProperty("distanceKm").TryGetInt32(out var km) && km is >= 2 and <= 8, $"distance {hit.GetProperty("distanceKm")}");
        Assert.Equal("Wicker Park, Chicago", hit.GetProperty("areaName").GetString());
    }

    [Fact]
    public async Task Near_you_ships_to_you_and_pickup_only_items_outside_the_radius()
    {
        var alice = await app.SignInAsync("alice");
        var tag = Guid.NewGuid().ToString("N")[..6];
        var (nearPickup, _) = await app.PublishAsync(alice, title: $"Near pickup {tag}", delivery: "pickup", placeId: 101);      // Wicker Park
        var (farPickup, _) = await app.PublishAsync(alice, title: $"Far pickup {tag}", delivery: "pickup", placeId: 104);        // Naperville
        var (farShips, _) = await app.PublishAsync(alice, title: $"Far ships {tag}", delivery: "both", placeId: 104);

        var feed = await (await app.CreateClient().GetAsync($"/v1/feed/nearby?lat={BuyerLat}&lng={BuyerLng}&radiusKm=25")).ReadAsync();
        var near = Ids(feed.GetProperty("near"));
        var ships = Ids(feed.GetProperty("shipsToYou"));

        Assert.Contains(nearPickup, near);
        Assert.DoesNotContain(farPickup, near);
        Assert.DoesNotContain(farPickup, ships);   // pickup-only and outside the radius: hidden
        Assert.Contains(farShips, ships);

        // A bigger radius brings Naperville in, nearest first.
        var wide = await (await app.CreateClient().GetAsync($"/v1/search?q={tag}&lat={BuyerLat}&lng={BuyerLng}&radiusKm=50&sort=nearest")).ReadAsync();
        var order = Ids(wide);
        Assert.Equal(nearPickup, order[0]);
        Assert.Contains(farPickup, order);

        // Turning off "include items that ship" keeps only what's in the radius.
        var localOnly = await (await app.CreateClient().GetAsync($"/v1/search?q={tag}&lat={BuyerLat}&lng={BuyerLng}&radiusKm=25&includeShipping=false")).ReadAsync();
        Assert.Equal([nearPickup], Ids(localOnly));
    }

    private static List<Guid> Ids(JsonElement cards) =>
        cards.EnumerateArray().Select(c => c.GetProperty("listingId").GetGuid()).ToList();
}
