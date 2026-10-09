using Marketplace.SharedKernel;

namespace Marketplace.Modules.Auctions.Domain;

internal enum AuctionStatus
{
    Live,
    ClosedSold,
    ClosedUnsold,
}

internal enum BidKind
{
    Manual,
    Proxy,
    BuyNow,
}

/// <summary>One accepted bid row. <see cref="MaxAmount"/> is private to the bidder and the engine.</summary>
internal sealed class Bid
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid AuctionId { get; init; }
    public Guid BidderId { get; init; }
    public required string BidderHandle { get; init; }
    /// <summary>The visible amount this row stands for in the bid history.</summary>
    public decimal Amount { get; init; }
    public decimal MaxAmount { get; init; }
    public BidKind Kind { get; init; }
    public long Seq { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <param name="Bids">Rows to insert, in seq order (a challenger's bid plus any automatic proxy bid).</param>
/// <param name="OutbidUserId">The previous leader, if they just lost the lead.</param>
internal sealed record BidOutcome(IReadOnlyList<Bid> Bids, Guid? OutbidUserId, bool Extended);

/// <summary>
/// An English auction with proxy bidding (feature design §4). All state changes happen while the
/// auction row is locked (single writer per auction, system design §3), so the methods here are plain,
/// synchronous and deterministic: time is always passed in from <see cref="IClock"/>.
/// </summary>
internal sealed class Auction
{
    public static readonly TimeSpan DefaultSoftClose = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan DefaultMaxExtension = TimeSpan.FromMinutes(30);

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ListingId { get; init; }
    public Guid SellerId { get; init; }
    public required string SellerHandle { get; init; }
    public required string Title { get; init; }
    public required string Currency { get; init; }

    public decimal StartPrice { get; init; }
    /// <summary>Hidden. Buyers only learn whether it's met.</summary>
    public decimal? ReservePrice { get; init; }
    public decimal? BuyNowPrice { get; init; }

    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; private set; }
    public DateTimeOffset OriginalEndsAt { get; init; }
    public TimeSpan SoftClose { get; init; } = DefaultSoftClose;
    public TimeSpan MaxExtension { get; init; } = DefaultMaxExtension;

    public decimal CurrentPrice { get; private set; }
    public Guid? LeaderId { get; private set; }
    public string? LeaderHandle { get; private set; }
    /// <summary>The leader's proxy maximum. Never shown to anyone but the leader.</summary>
    public decimal? LeaderMax { get; private set; }
    public int BidCount { get; private set; }
    /// <summary>Monotonic per auction; every bid row and state change gets the next value.</summary>
    public long LastSeq { get; private set; }

    public AuctionStatus Status { get; private set; } = AuctionStatus.Live;
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? WinnerId { get; private set; }
    public decimal? FinalPrice { get; private set; }
    public string? CloseReason { get; private set; }

    public static Auction Start(
        Guid listingId, Guid sellerId, string sellerHandle, string title, string currency,
        decimal startPrice, decimal? reservePrice, decimal? buyNowPrice, DateTimeOffset startsAt, DateTimeOffset endsAt) => new()
    {
        ListingId = listingId,
        SellerId = sellerId,
        SellerHandle = sellerHandle,
        Title = title,
        Currency = currency,
        StartPrice = startPrice,
        ReservePrice = reservePrice,
        BuyNowPrice = buyNowPrice,
        StartsAt = startsAt,
        EndsAt = endsAt,
        OriginalEndsAt = endsAt,
        CurrentPrice = startPrice,
    };

    public bool HasReserve => ReservePrice is not null;
    public bool ReserveMet => ReservePrice is null || (LeaderId is not null && CurrentPrice >= ReservePrice);
    public bool BuyNowAvailable => Status == AuctionStatus.Live && BuyNowPrice is not null && BidCount == 0;
    public decimal MinNextBid => LeaderId is null ? StartPrice : CurrentPrice + IncrementTable.For(CurrentPrice);

    /// <summary>Proxy bid resolution, feature design §4.1 rules 1–5, then soft close (§4.4).</summary>
    public BidOutcome PlaceBid(Guid bidderId, string bidderHandle, decimal maxAmount, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (bidderId == SellerId)
            throw ProblemException.Forbidden("own_auction", "You can't bid on your own auction.");
        if (maxAmount <= 0 || decimal.Round(maxAmount, 2) != maxAmount)
            throw ProblemException.BadRequest("invalid_amount", "Bid a positive amount with at most 2 decimals.");

        var bids = new List<Bid>();
        Guid? outbid = null;

        if (LeaderId == bidderId)
        {
            // The leader raises their own max: the visible price doesn't move (except for the reserve jump).
            if (maxAmount <= LeaderMax)
                throw ProblemException.BadRequest("max_not_higher", $"You're already winning; a new maximum must be above your current {LeaderMax:0.00}.");
            LeaderMax = maxAmount;
            ApplyReserveJump();
            bids.Add(NewBid(bidderId, bidderHandle, CurrentPrice, maxAmount, BidKind.Manual, now));
        }
        else if (LeaderId is null)
        {
            if (maxAmount < StartPrice)
                throw ProblemException.BadRequest("bid_too_low", $"Bid at least {StartPrice:0.00}.");
            (LeaderId, LeaderHandle, LeaderMax, CurrentPrice) = (bidderId, bidderHandle, maxAmount, StartPrice);
            ApplyReserveJump();
            bids.Add(NewBid(bidderId, bidderHandle, CurrentPrice, maxAmount, BidKind.Manual, now));
        }
        else
        {
            var minimum = MinNextBid;
            if (maxAmount < minimum)
                throw ProblemException.BadRequest("bid_too_low", $"Bid at least {minimum:0.00}.");

            var leaderMax = LeaderMax!.Value;
            if (maxAmount > leaderMax)
            {
                // Rule 2: new leader at min(M_new, M_lead + inc(M_lead)). Show the old leader's proxy reaching their max.
                if (leaderMax > CurrentPrice)
                    bids.Add(NewBid(LeaderId.Value, LeaderHandle!, leaderMax, leaderMax, BidKind.Proxy, now));
                outbid = LeaderId;
                (LeaderId, LeaderHandle, LeaderMax) = (bidderId, bidderHandle, maxAmount);
                CurrentPrice = Math.Min(maxAmount, leaderMax + IncrementTable.For(leaderMax));
                ApplyReserveJump();
                bids.Add(NewBid(bidderId, bidderHandle, CurrentPrice, maxAmount, BidKind.Manual, now));
            }
            else
            {
                // Rules 3–4: the leader holds (equal maxima: the earlier bid wins) at min(M_lead, M_new + inc(M_new)).
                bids.Add(NewBid(bidderId, bidderHandle, maxAmount, maxAmount, BidKind.Manual, now));
                CurrentPrice = Math.Min(leaderMax, maxAmount + IncrementTable.For(maxAmount));
                ApplyReserveJump();
                bids.Add(NewBid(LeaderId.Value, LeaderHandle!, CurrentPrice, leaderMax, BidKind.Proxy, now));
            }
        }

        BidCount += bids.Count;
        return new BidOutcome(bids, outbid, ApplySoftClose(now));
    }

    /// <summary>Buy Now is offered until the first bid (feature design §3) and ends the auction as sold.</summary>
    public Bid BuyNow(Guid buyerId, string buyerHandle, DateTimeOffset now)
    {
        EnsureOpen(now);
        if (buyerId == SellerId)
            throw ProblemException.Forbidden("own_auction", "You can't buy your own item.");
        if (!BuyNowAvailable)
            throw ProblemException.Conflict("buy_now_unavailable", "Buy Now is no longer available for this auction.");

        var price = BuyNowPrice!.Value;
        (LeaderId, LeaderHandle, LeaderMax, CurrentPrice) = (buyerId, buyerHandle, price, price);
        var bid = NewBid(buyerId, buyerHandle, price, price, BidKind.BuyNow, now);
        BidCount++;
        CloseAs(sold: true, "buy_now", now);
        return bid;
    }

    /// <summary>Idempotent: does nothing unless the auction is live and past its (possibly extended) end.</summary>
    public bool Close(DateTimeOffset now)
    {
        if (Status != AuctionStatus.Live || now < EndsAt)
            return false;
        CloseAs(sold: LeaderId is not null && ReserveMet, "ended", now);
        return true;
    }

    private void CloseAs(bool sold, string reason, DateTimeOffset now)
    {
        Status = sold ? AuctionStatus.ClosedSold : AuctionStatus.ClosedUnsold;
        (ClosedAt, CloseReason) = (now, reason);
        WinnerId = sold ? LeaderId : null;
        FinalPrice = sold ? CurrentPrice : null;
        LastSeq++;
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        if (Status != AuctionStatus.Live || now >= EndsAt)
            throw ProblemException.Conflict("auction_closed", "This auction has ended.");
        if (now < StartsAt)
            throw ProblemException.Conflict("auction_not_started", "This auction hasn't started yet.");
    }

    /// <summary>Rule 5: once a maximum reaches an unmet reserve, the price jumps to the reserve.</summary>
    private void ApplyReserveJump()
    {
        if (ReservePrice is { } reserve && CurrentPrice < reserve && LeaderMax >= reserve)
            CurrentPrice = reserve;
    }

    /// <summary>A bid in the last <see cref="SoftClose"/> moves the end to now + <see cref="SoftClose"/>, capped by <see cref="MaxExtension"/>.</summary>
    private bool ApplySoftClose(DateTimeOffset now)
    {
        if (EndsAt - now > SoftClose)
            return false;
        var newEnd = Min(now + SoftClose, OriginalEndsAt + MaxExtension);
        if (newEnd <= EndsAt)
            return false;
        EndsAt = newEnd;
        return true;
    }

    private Bid NewBid(Guid bidderId, string handle, decimal amount, decimal max, BidKind kind, DateTimeOffset now) => new()
    {
        AuctionId = Id,
        BidderId = bidderId,
        BidderHandle = handle,
        Amount = amount,
        MaxAmount = max,
        Kind = kind,
        Seq = ++LastSeq,
        CreatedAt = now,
    };

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
