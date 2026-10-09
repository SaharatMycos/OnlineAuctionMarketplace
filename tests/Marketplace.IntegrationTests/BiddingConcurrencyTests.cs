using System.Net;
using Marketplace.SharedKernel.Events;

namespace Marketplace.IntegrationTests;

/// <summary>Single writer per auction (system design §3, §10): parallel bids, retries, closing and realtime fan-out.</summary>
[Collection(MarketplaceCollection.Name)]
public class BiddingConcurrencyTests(MarketplaceFixture app)
{
    [Fact]
    public async Task Fifty_parallel_bids_produce_a_correct_serial_outcome()
    {
        var seller = await app.SignInAsync("alice");
        var bidders = await Task.WhenAll(Enumerable.Range(1, 5).Select(i => app.SignInAsync($"racer{i}")));
        var (_, auctionId) = await app.PublishAsync(seller, startPrice: 10);

        var random = new Random(42);
        var attempts = Enumerable.Range(0, 50)
            .Select(i => (Client: bidders[i % bidders.Length], Max: 10m + random.Next(0, 2000) / 4m))
            .ToList();

        var responses = await Task.WhenAll(attempts.Select(a => a.Client.BidAsync(auctionId, a.Max)));

        // Every request either succeeded or was a clean rule rejection; never a server error.
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {(int)r.StatusCode}"));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

        var history = await (await seller.GetAsync($"/v1/auctions/{auctionId}/bids")).ReadAsync();
        var seqs = history.EnumerateArray().Select(b => b.GetProperty("seq").GetInt64()).OrderBy(s => s).ToList();
        var auction = await seller.GetAuctionAsync(auctionId);

        // No lost or duplicated bids: the (latest 50) seqs are unique and contiguous, and the newest equals the bid count.
        Assert.Equal(seqs.Distinct().Count(), seqs.Count);
        Assert.Equal(Enumerable.Range((int)seqs[0], seqs.Count).Select(i => (long)i), seqs);
        Assert.Equal(auction.GetProperty("bidCount").GetInt32(), auction.GetProperty("seq").GetInt64());
        Assert.Equal(seqs[^1], auction.GetProperty("seq").GetInt64());

        // The leader holds the highest accepted max.
        var accepted = attempts.Zip(responses).Where(x => x.Second.StatusCode == HttpStatusCode.OK).Select(x => x.First.Max).ToList();
        var leaderViews = await Task.WhenAll(bidders.Select(b => b.GetAuctionAsync(auctionId)));
        var leaderView = Assert.Single(leaderViews, v => v.GetProperty("you").GetProperty("isLeader").GetBoolean());
        Assert.Equal(accepted.Max(), leaderView.GetProperty("you").GetProperty("yourMax").GetDecimal());
    }

    [Fact]
    public async Task Retrying_with_the_same_idempotency_key_never_bids_twice()
    {
        var seller = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var (_, auctionId) = await app.PublishAsync(seller, startPrice: 20);

        var key = Guid.NewGuid().ToString();
        var first = await (await bob.BidAsync(auctionId, 50, key)).ReadAsync();
        var retries = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => bob.BidAsync(auctionId, 50, key)));

        foreach (var retry in retries)
            Assert.Equal(first.GetRawText(), (await retry.ReadAsync()).GetRawText());
        Assert.Equal(1, (await bob.GetAuctionAsync(auctionId)).GetProperty("bidCount").GetInt32());
    }

    [Fact]
    public async Task Close_job_respects_soft_close_and_is_idempotent()
    {
        var seller = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var carol = await app.SignInAsync("carol");
        var (_, auctionId) = await app.PublishAsync(seller, startPrice: 20, durationMinutes: 5);

        var originalEnd = (await bob.GetAuctionAsync(auctionId)).GetProperty("endsAt").GetDateTimeOffset();
        await (await bob.BidAsync(auctionId, 30)).ReadAsync();

        // A bid 30 s before the end extends it to now + 2 min.
        app.Clock.Set(originalEnd.AddSeconds(-30));
        var late = await (await carol.BidAsync(auctionId, 60)).ReadAsync();
        Assert.True(late.GetProperty("extended").GetBoolean());
        var extendedEnd = late.GetProperty("endsAt").GetDateTimeOffset();
        Assert.Equal(app.Clock.UtcNow.AddMinutes(2), extendedEnd);

        // At the original end nothing closes; after the extended end it does, exactly once.
        app.Clock.Set(originalEnd.AddSeconds(1));
        await app.CloseDueAuctionsAsync();
        Assert.Equal("live", (await bob.GetAuctionAsync(auctionId)).GetProperty("status").GetString());

        app.Clock.Set(extendedEnd.AddSeconds(1));
        await app.CloseDueAuctionsAsync();
        Assert.Equal("sold", (await bob.GetAuctionAsync(auctionId)).GetProperty("status").GetString());
        Assert.Equal(0, await app.CloseDueAuctionsAsync());

        var order = await (await carol.GetAsync($"/v1/orders/by-auction/{auctionId}")).ReadAsync();
        Assert.Equal("buyer", order.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Bids_are_pushed_to_watchers_and_the_outbid_user()
    {
        var seller = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var carol = await app.SignInAsync("carol");
        var bobId = (await (await bob.GetAsync("/v1/me")).ReadAsync()).GetProperty("id").GetGuid();
        var (_, auctionId) = await app.PublishAsync(seller, startPrice: 20);

        await (await bob.BidAsync(auctionId, 40)).ReadAsync();
        await (await carol.BidAsync(auctionId, 90)).ReadAsync();
        await app.DrainOutboxAsync();

        var pushes = app.Realtime.Pushes.ToList();
        Assert.Contains(pushes, p => p.Group == RealtimeGroups.Auction(auctionId) && p.Event == "bid.placed");
        Assert.Contains(pushes, p => p.Group == RealtimeGroups.User(bobId) && p.Event == "user.outbid");
    }
}
