using System.Net;
using System.Net.Http.Json;

namespace Marketplace.IntegrationTests;

/// <summary>The MVP loop end to end: publish → auction → bids → close → order → agreement → paid → received.</summary>
[Collection(MarketplaceCollection.Name)]
public class CoreLoopTests(MarketplaceFixture app)
{
    [Fact]
    public async Task Sell_bid_win_agree_pay_receive()
    {
        var alice = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var carol = await app.SignInAsync("carol");
        var (listingId, auctionId) = await app.PublishAsync(alice, startPrice: 150);

        // Bidding: bob leads, carol challenges lower and is auto-outbid, then bob is the winner.
        await (await bob.BidAsync(auctionId, 400)).ReadAsync();
        var carolBid = await (await carol.BidAsync(auctionId, 320)).ReadAsync();
        Assert.False(carolBid.GetProperty("isLeader").GetBoolean());
        Assert.Equal(325m, carolBid.GetProperty("currentPrice").GetDecimal());    // 320 + inc(320) = 5

        var view = await bob.GetAuctionAsync(auctionId);
        Assert.True(view.GetProperty("you").GetProperty("isLeader").GetBoolean());
        Assert.Equal(400m, view.GetProperty("you").GetProperty("yourMax").GetDecimal());

        // Close, then the order and deal agreement appear for both parties.
        await app.EndAuctionAsync(bob, auctionId);
        var closed = await bob.GetAuctionAsync(auctionId);
        Assert.Equal("sold", closed.GetProperty("status").GetString());

        var order = await (await bob.GetAsync($"/v1/orders/by-auction/{auctionId}")).ReadAsync();
        var orderId = order.GetProperty("id").GetGuid();
        var agreement = order.GetProperty("agreement");
        Assert.Equal("agreement_pending", order.GetProperty("status").GetString());
        Assert.StartsWith("AG-", agreement.GetProperty("number").GetString());
        Assert.Equal(325m, agreement.GetProperty("agreedPrice").GetDecimal());
        Assert.Equal("pickup", agreement.GetProperty("handover").GetString());
        Assert.Equal("cash", agreement.GetProperty("paymentMethod").GetString());
        Assert.Equal(JsonValueKindNull, agreement.GetProperty("pickupAddress").ValueKind);

        // Only the two parties can read it.
        Assert.Equal("not_a_party", await (await carol.GetAsync($"/v1/orders/{orderId}")).ProblemCodeAsync(HttpStatusCode.Forbidden));

        // Seller accepts v1; buyer changes the date (v2) which resets the seller's acceptance.
        await (await alice.PostAsJsonAsync($"/v1/orders/{orderId}/agreement/accept", new { version = 1 })).ReadAsync();
        var changed = await (await bob.PatchAsJsonAsync($"/v1/orders/{orderId}/agreement",
            new { version = 1, handoverDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime.AddDays(5)), notes = "Evening pickup please" })).ReadAsync();
        Assert.Equal(2, changed.GetProperty("agreement").GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKindNull, changed.GetProperty("agreement").GetProperty("sellerAcceptedAt").ValueKind);

        // Stale version is rejected; the price can't be changed through the API at all.
        Assert.Equal("stale_version", await (await alice.PostAsJsonAsync($"/v1/orders/{orderId}/agreement/accept", new { version = 1 }))
            .ProblemCodeAsync(HttpStatusCode.Conflict));

        await (await alice.PostAsJsonAsync($"/v1/orders/{orderId}/agreement/accept", new { version = 2 })).ReadAsync();
        var agreed = await (await bob.PostAsJsonAsync($"/v1/orders/{orderId}/agreement/accept", new { version = 2 })).ReadAsync();
        Assert.Equal("agreed", agreed.GetProperty("status").GetString());
        Assert.Equal(325m, agreed.GetProperty("agreement").GetProperty("agreedPrice").GetDecimal());

        // The exact pickup address is revealed only now.
        Assert.Equal(Scenario.SecretAddress, agreed.GetProperty("agreement").GetProperty("pickupAddress").GetString());

        // Both confirm payment, then the buyer marks it received.
        Assert.Equal("not_paid", await (await bob.PostAsync($"/v1/orders/{orderId}/mark-received", null)).ProblemCodeAsync(HttpStatusCode.Conflict));
        var buyerPaid = await (await bob.PostAsync($"/v1/orders/{orderId}/confirm-paid", null)).ReadAsync();
        Assert.Equal("agreed", buyerPaid.GetProperty("status").GetString());
        var dealPaid = await (await alice.PostAsync($"/v1/orders/{orderId}/confirm-paid", null)).ReadAsync();
        Assert.Equal("deal_paid", dealPaid.GetProperty("status").GetString());
        var received = await (await bob.PostAsync($"/v1/orders/{orderId}/mark-received", null)).ReadAsync();
        Assert.Equal("received", received.GetProperty("status").GetString());

        // History is complete and append-only.
        var actions = received.GetProperty("history").EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Equal(["created", "accepted", "terms_changed", "accepted", "accepted", "confirmed_paid", "confirmed_received", "marked_received"], actions);

        // The listing ended and both sides see the order in their lists.
        var listing = await (await alice.GetAsync($"/v1/listings/{listingId}")).ReadAsync();
        Assert.Equal("ended", listing.GetProperty("status").GetString());
        var sellerOrders = await (await alice.GetAsync("/v1/me/orders?role=seller")).ReadAsync();
        Assert.Contains(sellerOrders.EnumerateArray(), o => o.GetProperty("id").GetGuid() == orderId);
        var bidding = await (await bob.GetAsync("/v1/me/bidding?status=won")).ReadAsync();
        Assert.Contains(bidding.EnumerateArray(), b => b.GetProperty("auctionId").GetGuid() == auctionId);
    }

    [Fact]
    public async Task Buy_now_sells_immediately_and_creates_the_agreement()
    {
        var alice = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var (_, auctionId) = await app.PublishAsync(alice, startPrice: 100, buyNow: 250, delivery: "both");

        var bought = await (await bob.PostAsync($"/v1/auctions/{auctionId}/buy-now", null)).ReadAsync();
        Assert.Equal("sold", bought.GetProperty("status").GetString());
        await app.DrainOutboxAsync();

        var order = await (await alice.GetAsync($"/v1/orders/by-auction/{auctionId}")).ReadAsync();
        Assert.Equal(250m, order.GetProperty("agreement").GetProperty("agreedPrice").GetDecimal());
        Assert.Equal("seller", order.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Unmet_reserve_ends_unsold_without_an_order()
    {
        var alice = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var (_, auctionId) = await app.PublishAsync(alice, startPrice: 50, reserve: 500);

        await (await bob.BidAsync(auctionId, 120)).ReadAsync();
        var view = await bob.GetAuctionAsync(auctionId);
        Assert.False(view.GetProperty("reserveMet").GetBoolean());

        await app.EndAuctionAsync(bob, auctionId);
        Assert.Equal("unsold", (await bob.GetAuctionAsync(auctionId)).GetProperty("status").GetString());
        await (await bob.GetAsync($"/v1/orders/by-auction/{auctionId}")).ReadAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rule_violations_return_problem_codes()
    {
        var alice = await app.SignInAsync("alice");
        var bob = await app.SignInAsync("bob");
        var (_, auctionId) = await app.PublishAsync(alice, startPrice: 100);

        Assert.Equal("own_auction", await (await alice.BidAsync(auctionId, 200)).ProblemCodeAsync(HttpStatusCode.Forbidden));
        Assert.Equal("bid_too_low", await (await bob.BidAsync(auctionId, 99)).ProblemCodeAsync(HttpStatusCode.BadRequest));

        var noKey = await bob.PostAsJsonAsync($"/v1/auctions/{auctionId}/bids", new { maxAmount = 120 });
        Assert.Equal("idempotency_key_required", await noKey.ProblemCodeAsync(HttpStatusCode.BadRequest));

        var anonymous = app.CreateClient();
        await (await anonymous.BidAsync(auctionId, 150)).ReadAsync(HttpStatusCode.Unauthorized);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;
}
