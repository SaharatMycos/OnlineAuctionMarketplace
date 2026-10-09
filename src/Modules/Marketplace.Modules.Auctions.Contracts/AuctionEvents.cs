using Marketplace.SharedKernel.Events;

namespace Marketplace.Modules.Auctions.Contracts;

[IntegrationEvent("auctions.auction_started.v1")]
public sealed record AuctionStarted(Guid AuctionId, Guid ListingId, decimal StartPrice, DateTimeOffset EndsAt, bool BuyNowAvailable) : IIntegrationEvent;

/// <summary>
/// A bid changed the auction. <paramref name="LeaderHandle"/> is already masked (e.g. <c>a***e</c>).
/// Proxy maxima are never part of this event.
/// </summary>
[IntegrationEvent("auctions.bid_placed.v1")]
public sealed record BidPlaced(
    Guid AuctionId,
    Guid ListingId,
    string Title,
    long Seq,
    decimal Price,
    int BidCount,
    DateTimeOffset EndsAt,
    bool Extended,
    bool BuyNowAvailable,
    string LeaderHandle,
    Guid? OutbidUserId) : IIntegrationEvent, IRealtimeEvent
{
    public IEnumerable<RealtimePush> RealtimePushes()
    {
        yield return new RealtimePush(RealtimeGroups.Auction(AuctionId), "bid.placed",
            new { AuctionId, ListingId, Seq, Price, BidCount, EndsAt, BuyNowAvailable, Leader = LeaderHandle });
        if (Extended)
            yield return new RealtimePush(RealtimeGroups.Auction(AuctionId), "auction.extended", new { AuctionId, Seq, EndsAt });
        if (OutbidUserId is { } outbid)
            yield return new RealtimePush(RealtimeGroups.User(outbid), "user.outbid", new { AuctionId, ListingId, Title, Price });
    }
}

[IntegrationEvent("auctions.auction_closed.v1")]
public sealed record AuctionClosed(
    Guid AuctionId,
    Guid ListingId,
    string Title,
    Guid SellerId,
    string SellerHandle,
    bool Sold,
    Guid? WinnerId,
    string? WinnerHandle,
    decimal? FinalPrice,
    string Currency,
    string Reason,
    DateTimeOffset ClosedAt,
    long Seq) : IIntegrationEvent, IRealtimeEvent
{
    public IEnumerable<RealtimePush> RealtimePushes()
    {
        yield return new RealtimePush(RealtimeGroups.Auction(AuctionId), "auction.closed",
            new { AuctionId, ListingId, Seq, Sold, FinalPrice, Reason });
        yield return new RealtimePush(RealtimeGroups.User(SellerId), "auction.ended",
            new { AuctionId, ListingId, Title, Sold, FinalPrice });
        if (WinnerId is { } winner)
            yield return new RealtimePush(RealtimeGroups.User(winner), "user.won",
                new { AuctionId, ListingId, Title, FinalPrice });
    }
}
