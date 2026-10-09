using Marketplace.Modules.Auctions.Domain;
using Marketplace.SharedKernel;

namespace Marketplace.UnitTests.Auctions;

/// <summary>Feature design §4.1 rules 1–5, §4.4 soft close, §3 Buy Now, and closing.</summary>
public class AuctionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Seller = Guid.NewGuid(), A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private static Auction NewAuction(decimal start = 10, decimal? reserve = null, decimal? buyNow = null, TimeSpan? length = null) =>
        Auction.Start(Guid.NewGuid(), Seller, "seller", "Trek FX 3", "USD", start, reserve, buyNow, T0, T0 + (length ?? TimeSpan.FromDays(1)));

    [Fact]
    public void First_bid_leads_at_the_start_price()
    {
        var auction = NewAuction(start: 10);
        auction.PlaceBid(A, "alice", 50, T0.AddMinutes(1));

        Assert.Equal(A, auction.LeaderId);
        Assert.Equal(10, auction.CurrentPrice);
        Assert.Equal(10.50m, auction.MinNextBid);
    }

    [Fact]
    public void Lower_challenger_is_outbid_automatically_at_one_increment_above()
    {
        var auction = NewAuction(start: 10);
        auction.PlaceBid(A, "alice", 50, T0.AddMinutes(1));
        var outcome = auction.PlaceBid(B, "bob", 30, T0.AddMinutes(2));

        Assert.Equal(A, auction.LeaderId);
        Assert.Equal(31, auction.CurrentPrice);                    // min(50, 30 + inc(30) = 1)
        Assert.Null(outcome.OutbidUserId);
        Assert.Equal([BidKind.Manual, BidKind.Proxy], outcome.Bids.Select(b => b.Kind));
    }

    [Fact]
    public void Higher_challenger_takes_the_lead_at_one_increment_above_the_old_max()
    {
        var auction = NewAuction(start: 10);
        auction.PlaceBid(A, "alice", 50, T0.AddMinutes(1));
        var outcome = auction.PlaceBid(B, "bob", 60, T0.AddMinutes(2));

        Assert.Equal(B, auction.LeaderId);
        Assert.Equal(51, auction.CurrentPrice);                    // min(60, 50 + inc(50) = 1)
        Assert.Equal(A, outcome.OutbidUserId);
    }

    [Fact]
    public void Equal_maxima_go_to_the_earlier_bid()
    {
        var auction = NewAuction(start: 10);
        auction.PlaceBid(A, "alice", 60, T0.AddMinutes(1));
        auction.PlaceBid(B, "bob", 60, T0.AddMinutes(2));

        Assert.Equal(A, auction.LeaderId);
        Assert.Equal(60, auction.CurrentPrice);
    }

    [Fact]
    public void Bid_below_the_minimum_is_rejected()
    {
        var auction = NewAuction(start: 10);
        auction.PlaceBid(A, "alice", 50, T0.AddMinutes(1));

        var error = Assert.Throws<ProblemException>(() => auction.PlaceBid(B, "bob", 10.25m, T0.AddMinutes(2)));
        Assert.Equal("bid_too_low", error.Code);
        Assert.Equal(1, auction.BidCount);
    }

    [Fact]
    public void Leader_can_raise_their_max_without_moving_the_price()
    {
        var auction = NewAuction(start: 10);
        auction.PlaceBid(A, "alice", 50, T0.AddMinutes(1));
        auction.PlaceBid(B, "bob", 30, T0.AddMinutes(2));
        auction.PlaceBid(A, "alice", 80, T0.AddMinutes(3));

        Assert.Equal(31, auction.CurrentPrice);
        Assert.Equal(80, auction.LeaderMax);
        Assert.Throws<ProblemException>(() => auction.PlaceBid(A, "alice", 70, T0.AddMinutes(4)));
    }

    [Fact]
    public void Price_jumps_to_the_reserve_once_a_max_reaches_it()
    {
        var auction = NewAuction(start: 10, reserve: 100);
        auction.PlaceBid(A, "alice", 60, T0.AddMinutes(1));
        Assert.False(auction.ReserveMet);
        Assert.Equal(10, auction.CurrentPrice);

        auction.PlaceBid(B, "bob", 150, T0.AddMinutes(2));
        Assert.True(auction.ReserveMet);
        Assert.Equal(100, auction.CurrentPrice);                   // not 61: rule 5
    }

    [Fact]
    public void Seller_cannot_bid_and_closed_auctions_reject_bids()
    {
        var auction = NewAuction();
        Assert.Equal("own_auction", Assert.Throws<ProblemException>(() => auction.PlaceBid(Seller, "seller", 20, T0.AddMinutes(1))).Code);
        Assert.Equal("auction_closed", Assert.Throws<ProblemException>(() => auction.PlaceBid(A, "alice", 20, T0.AddDays(2))).Code);
    }

    [Fact]
    public void Bid_in_the_last_two_minutes_extends_the_end()
    {
        var auction = NewAuction(length: TimeSpan.FromMinutes(10));
        var end = auction.EndsAt;

        var early = auction.PlaceBid(A, "alice", 20, end.AddMinutes(-5));
        Assert.False(early.Extended);

        var late = auction.PlaceBid(B, "bob", 30, end.AddSeconds(-30));
        Assert.True(late.Extended);
        Assert.Equal(end.AddSeconds(-30).AddMinutes(2), auction.EndsAt);
    }

    [Fact]
    public void Extensions_stop_at_the_maximum_total_extension()
    {
        var auction = NewAuction(length: TimeSpan.FromMinutes(10));
        var cap = auction.OriginalEndsAt + Auction.DefaultMaxExtension;

        decimal max = 20;
        var bidders = new[] { (A, "alice"), (B, "bob") };
        for (var i = 0; auction.EndsAt < cap; i++)
        {
            var now = auction.EndsAt.AddSeconds(-10);
            var (id, handle) = bidders[i % 2];
            auction.PlaceBid(id, handle, max += 10, now);
        }

        Assert.Equal(cap, auction.EndsAt);
        var atCap = auction.PlaceBid(auction.LeaderId == A ? B : A, "x", max += 10, cap.AddSeconds(-5));
        Assert.False(atCap.Extended);
        Assert.Equal(cap, auction.EndsAt);
    }

    [Fact]
    public void Buy_now_is_available_until_the_first_bid()
    {
        var auction = NewAuction(start: 10, buyNow: 200);
        Assert.True(auction.BuyNowAvailable);

        auction.PlaceBid(A, "alice", 20, T0.AddMinutes(1));
        Assert.False(auction.BuyNowAvailable);
        Assert.Equal("buy_now_unavailable", Assert.Throws<ProblemException>(() => auction.BuyNow(B, "bob", T0.AddMinutes(2))).Code);
    }

    [Fact]
    public void Buy_now_closes_the_auction_as_sold()
    {
        var auction = NewAuction(start: 10, buyNow: 200);
        auction.BuyNow(B, "bob", T0.AddMinutes(1));

        Assert.Equal(AuctionStatus.ClosedSold, auction.Status);
        Assert.Equal(B, auction.WinnerId);
        Assert.Equal(200, auction.FinalPrice);
        Assert.Equal("buy_now", auction.CloseReason);
    }

    [Fact]
    public void Close_is_idempotent_and_respects_the_reserve()
    {
        var sold = NewAuction(start: 10);
        sold.PlaceBid(A, "alice", 20, T0.AddMinutes(1));
        Assert.False(sold.Close(sold.EndsAt.AddSeconds(-1)));       // not yet
        Assert.True(sold.Close(sold.EndsAt));
        Assert.False(sold.Close(sold.EndsAt.AddMinutes(1)));        // already closed
        Assert.Equal(AuctionStatus.ClosedSold, sold.Status);
        Assert.Equal(A, sold.WinnerId);

        var reserveNotMet = NewAuction(start: 10, reserve: 100);
        reserveNotMet.PlaceBid(A, "alice", 50, T0.AddMinutes(1));
        Assert.True(reserveNotMet.Close(reserveNotMet.EndsAt));
        Assert.Equal(AuctionStatus.ClosedUnsold, reserveNotMet.Status);
        Assert.Null(reserveNotMet.WinnerId);

        var noBids = NewAuction();
        Assert.True(noBids.Close(noBids.EndsAt));
        Assert.Equal(AuctionStatus.ClosedUnsold, noBids.Status);
    }

    [Theory]
    [InlineData(0.99, 0.05)]
    [InlineData(1, 0.25)]
    [InlineData(4.99, 0.25)]
    [InlineData(5, 0.50)]
    [InlineData(24.99, 0.50)]
    [InlineData(25, 1)]
    [InlineData(99.99, 1)]
    [InlineData(100, 2.50)]
    [InlineData(249.99, 2.50)]
    [InlineData(250, 5)]
    [InlineData(500, 10)]
    [InlineData(1000, 25)]
    [InlineData(2500, 50)]
    [InlineData(4999.99, 50)]
    [InlineData(5000, 100)]
    [InlineData(1000000, 100)]
    public void Increment_table_boundaries(decimal price, decimal increment) =>
        Assert.Equal(increment, IncrementTable.For(price));
}
