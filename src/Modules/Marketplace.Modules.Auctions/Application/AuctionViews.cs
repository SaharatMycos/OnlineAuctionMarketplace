using Marketplace.Modules.Auctions.Domain;
using Marketplace.SharedKernel.Events;

namespace Marketplace.Modules.Auctions.Application;

/// <summary>What the caller knows about their own position. <c>YourMax</c> is only ever the caller's own.</summary>
internal sealed record YouView(bool IsSeller, bool HasBid, bool IsLeader, decimal? YourMax, bool Won);

/// <summary>Public auction state. No proxy maxima, no reserve amount, leader handle masked.</summary>
internal sealed record AuctionView(
    Guid Id,
    Guid ListingId,
    string Title,
    string SellerHandle,
    string Currency,
    string Status,
    decimal StartPrice,
    decimal CurrentPrice,
    decimal MinNextBid,
    int BidCount,
    decimal? BuyNowPrice,
    bool HasReserve,
    bool ReserveMet,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset ServerTime,
    string? Leader,
    decimal? FinalPrice,
    long Seq,
    YouView? You)
{
    public static string StatusOf(Auction a) => a.Status switch
    {
        AuctionStatus.Live => "live",
        AuctionStatus.ClosedSold => "sold",
        _ => "unsold",
    };

    public static AuctionView From(Auction a, DateTimeOffset now, YouView? you) => new(
        a.Id, a.ListingId, a.Title, a.SellerHandle, a.Currency, StatusOf(a),
        a.StartPrice, a.CurrentPrice, a.MinNextBid, a.BidCount,
        a.BuyNowAvailable ? a.BuyNowPrice : null,
        a.HasReserve, a.ReserveMet, a.StartsAt, a.EndsAt, now,
        a.LeaderHandle is null ? null : Handles.Mask(a.LeaderHandle),
        a.FinalPrice, a.LastSeq, you);
}

internal sealed record BidResponse(
    Guid AuctionId,
    bool IsLeader,
    decimal CurrentPrice,
    decimal MinNextBid,
    int BidCount,
    DateTimeOffset EndsAt,
    decimal YourMax,
    bool Extended,
    bool ReserveMet,
    string Status);

internal sealed record BidHistoryItem(long Seq, decimal Amount, string Bidder, string Kind, DateTimeOffset At);

internal sealed record MyBiddingItem(
    Guid AuctionId,
    Guid ListingId,
    string Title,
    string Currency,
    decimal CurrentPrice,
    decimal YourMax,
    int BidCount,
    DateTimeOffset EndsAt,
    string AuctionStatus,
    /// <summary>winning, outbid, won or lost.</summary>
    string Standing);
