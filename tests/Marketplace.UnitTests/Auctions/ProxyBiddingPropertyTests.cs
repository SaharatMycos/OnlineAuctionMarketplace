using Marketplace.Modules.Auctions.Domain;
using Marketplace.SharedKernel;

namespace Marketplace.UnitTests.Auctions;

/// <summary>
/// Property tests for proxy bidding (system design §10), with seeded random bid sequences so failures
/// reproduce. After every accepted bid:
/// <list type="bullet">
/// <item>the leader holds the highest maximum (ties: the earliest),</item>
/// <item>the price never exceeds the leader's maximum,</item>
/// <item>the price never exceeds the second-highest maximum + its increment (unless the reserve jump applies),</item>
/// <item>the price is at least the second-highest maximum (capped by the leader's),</item>
/// <item>bid seqs are strictly increasing and the bid count matches the rows produced.</item>
/// </list>
/// </summary>
public class ProxyBiddingPropertyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<int> Seeds
    {
        get
        {
            var data = new TheoryData<int>();
            for (var seed = 1; seed <= 300; seed++)
                data.Add(seed);
            return data;
        }
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Invariants_hold_for_random_bid_sequences(int seed)
    {
        var random = new Random(seed);
        var startPrice = Money(random, 1, 200);
        decimal? reserve = random.Next(3) == 0 ? startPrice + Money(random, 0, 300) : null;
        var auction = Auction.Start(Guid.NewGuid(), Guid.NewGuid(), "seller", "Item", "USD", startPrice, reserve, null, T0, T0.AddDays(7));

        var bidders = Enumerable.Range(0, random.Next(2, 6)).Select(_ => Guid.NewGuid()).ToArray();
        var maxima = new Dictionary<Guid, (decimal Max, long FirstReached)>();
        long lastSeq = 0, order = 0;
        var rows = 0;

        for (var step = 0; step < 60; step++)
        {
            var bidder = bidders[random.Next(bidders.Length)];
            // Mostly plausible bids around the current price, some too low, some far above.
            var max = Math.Round(auction.CurrentPrice + (decimal)(random.NextDouble() * 120 - 20), 2);
            var now = T0.AddMinutes(step + 1);

            BidOutcome outcome;
            try
            {
                outcome = auction.PlaceBid(bidder, $"b{bidder:N}"[..6], max, now);
            }
            catch (ProblemException)
            {
                continue;   // rejected (too low, not above own max, ...): state must be unchanged, checked below
            }

            order++;
            if (!maxima.TryGetValue(bidder, out var previous) || max > previous.Max)
                maxima[bidder] = (max, order);
            rows += outcome.Bids.Count;

            // Leader holds the highest max; ties go to whoever reached it first.
            var expectedLeader = maxima.OrderByDescending(m => m.Value.Max).ThenBy(m => m.Value.FirstReached).First();
            Assert.Equal(expectedLeader.Key, auction.LeaderId);
            Assert.Equal(expectedLeader.Value.Max, auction.LeaderMax);

            var leaderMax = auction.LeaderMax!.Value;
            Assert.True(auction.CurrentPrice <= leaderMax, $"seed {seed}: price {auction.CurrentPrice} > leader max {leaderMax}");

            var others = maxima.Where(m => m.Key != auction.LeaderId).Select(m => m.Value.Max).ToList();
            var reserveJumped = reserve is { } r && leaderMax >= r && auction.CurrentPrice == r;
            if (others.Count > 0)
            {
                var second = others.Max();
                if (!reserveJumped)
                    Assert.True(auction.CurrentPrice <= second + IncrementTable.For(second),
                        $"seed {seed}: price {auction.CurrentPrice} > second {second} + inc");
                Assert.True(auction.CurrentPrice >= Math.Min(second, leaderMax),
                    $"seed {seed}: price {auction.CurrentPrice} < second {second}");
            }
            else if (!reserveJumped)
            {
                Assert.Equal(startPrice, auction.CurrentPrice);
            }

            foreach (var bid in outcome.Bids)
            {
                Assert.True(bid.Seq > lastSeq);
                lastSeq = bid.Seq;
            }
            Assert.Equal(rows, auction.BidCount);
            Assert.Equal(lastSeq, auction.LastSeq);
        }
    }

    private static decimal Money(Random random, int min, int max) => Math.Round((decimal)(min + random.NextDouble() * (max - min)), 2);
}
