using System.Net;
using System.Text.Json;

namespace Marketplace.IntegrationTests;

[Collection(MarketplaceCollection.Name)]
public class AuctionCancellationTests(MarketplaceFixture app)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Seller_cancels_with_at_most_one_bidder_without_creating_a_deal(bool withBids)
    {
        var seller = await app.SignInAsync("cancel-seller");
        var bidder = await app.SignInAsync("cancel-bidder");
        var (listingId, id) = await app.PublishAsync(seller, startPrice: 10, buyNow: 100);
        if (withBids)
        {
            await (await bidder.BidAsync(id, 30)).ReadAsync();
            await (await bidder.BidAsync(id, 40)).ReadAsync();
        }
        Assert.True((await seller.GetAuctionAsync(id)).GetProperty("you").GetProperty("canCancel").GetBoolean());
        await (await bidder.PostAsync($"/v1/auctions/{id}/cancel", null)).ReadAsync(HttpStatusCode.Forbidden);
        var history = await (await seller.GetAsync($"/v1/auctions/{id}/bids")).ReadAsync();
        var cancelled = await (await seller.PostAsync($"/v1/auctions/{id}/cancel", null)).ReadAsync();
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cancelled.GetProperty("finalPrice").ValueKind);
        var retry = await (await seller.PostAsync($"/v1/auctions/{id}/cancel", null)).ReadAsync();
        Assert.Equal(cancelled.GetProperty("seq").GetInt64(), retry.GetProperty("seq").GetInt64());
        Assert.Equal(history.GetRawText(), (await (await seller.GetAsync($"/v1/auctions/{id}/bids")).ReadAsync()).GetRawText());
        await (await bidder.BidAsync(id, 50)).ReadAsync(HttpStatusCode.Conflict);
        await (await bidder.PostAsync($"/v1/auctions/{id}/buy-now", null)).ReadAsync(HttpStatusCode.Conflict);
        await app.DrainOutboxAsync();
        await app.DrainOutboxAsync();
        await (await seller.GetAsync($"/v1/orders/by-auction/{id}")).ReadAsync(HttpStatusCode.NotFound);
        var listing = await (await seller.GetAsync($"/v1/listings/{listingId}")).ReadAsync();
        Assert.Equal("ended", listing.GetProperty("status").GetString());
        var search = await (await seller.GetAsync("/v1/search")).ReadAsync();
        Assert.DoesNotContain(search.EnumerateArray(), c => c.GetProperty("listingId").GetGuid() == listingId);
        Assert.Single(app.Realtime.Pushes, p => p.Event == "auction.closed" &&
            JsonSerializer.SerializeToElement(p.Payload, MarketplaceFixture.Json).GetProperty("auctionId").GetGuid() == id);
        if (withBids)
        {
            var mine = await (await bidder.GetAsync("/v1/me/bidding")).ReadAsync();
            Assert.Equal("cancelled", mine.EnumerateArray().Single(a => a.GetProperty("auctionId").GetGuid() == id).GetProperty("standing").GetString());
        }
    }

    [Fact]
    public async Task Second_distinct_bidder_blocks_cancellation_even_below_reserve()
    {
        var seller = await app.SignInAsync("cancel-limit-seller");
        var bob = await app.SignInAsync("cancel-limit-bob");
        var carol = await app.SignInAsync("cancel-limit-carol");
        var (_, id) = await app.PublishAsync(seller, startPrice: 10, reserve: 100);
        await (await bob.BidAsync(id, 30)).ReadAsync();
        await (await carol.BidAsync(id, 20)).ReadAsync();
        Assert.False((await seller.GetAuctionAsync(id)).GetProperty("you").GetProperty("canCancel").GetBoolean());
        Assert.Equal("too_many_bidders", await (await seller.PostAsync($"/v1/auctions/{id}/cancel", null)).ProblemCodeAsync(HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_serializes_with_second_bidder_or_buy_now(bool buyNow)
    {
        var seller = await app.SignInAsync("cancel-race-seller");
        var bob = await app.SignInAsync("cancel-race-bob");
        var carol = await app.SignInAsync("cancel-race-carol");
        var (_, id) = await app.PublishAsync(seller, startPrice: 10, buyNow: 100);
        if (!buyNow) await (await bob.BidAsync(id, 30)).ReadAsync();
        var cancel = seller.PostAsync($"/v1/auctions/{id}/cancel", null);
        var competing = buyNow ? carol.PostAsync($"/v1/auctions/{id}/buy-now", null) : carol.BidAsync(id, 40);
        var responses = await Task.WhenAll(cancel, competing);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var state = await seller.GetAuctionAsync(id);
        Assert.Equal(responses[0].IsSuccessStatusCode ? "cancelled" : buyNow ? "sold" : "live", state.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancellation_requires_authentication_existing_auction_and_unexpired_time()
    {
        var seller = await app.SignInAsync("cancel-expiry-seller");
        await (await seller.PostAsync($"/v1/auctions/{Guid.NewGuid()}/cancel", null)).ReadAsync(HttpStatusCode.NotFound);
        var (_, id) = await app.PublishAsync(seller);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/v1/auctions/{id}/cancel", null)).StatusCode);
        app.Clock.Set((await seller.GetAuctionAsync(id)).GetProperty("endsAt").GetDateTimeOffset());
        await (await seller.PostAsync($"/v1/auctions/{id}/cancel", null)).ReadAsync(HttpStatusCode.Conflict);
        await app.CloseDueAuctionsAsync();
        await (await seller.PostAsync($"/v1/auctions/{id}/cancel", null)).ReadAsync(HttpStatusCode.Conflict);
    }
}
