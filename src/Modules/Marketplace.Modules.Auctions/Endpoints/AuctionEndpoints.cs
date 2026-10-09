using Marketplace.Modules.Auctions.Application;
using Marketplace.Modules.Auctions.Domain;
using Marketplace.Modules.Auctions.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Auth;
using Marketplace.SharedKernel.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Auctions.Endpoints;

internal static class AuctionEndpoints
{
    public sealed record PlaceBidRequest(decimal MaxAmount);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1").WithTags("Auctions");
        group.MapGet("/auctions/{id:guid}", GetAuction);
        group.MapGet("/auctions/by-listing/{listingId:guid}", GetAuctionByListing);
        group.MapGet("/auctions/{id:guid}/bids", GetBids);
        group.MapPost("/auctions/{id:guid}/bids", PlaceBid).RequireAuthorization();
        group.MapPost("/auctions/{id:guid}/buy-now", BuyNow).RequireAuthorization();
        group.MapGet("/me/bidding", GetMyBidding).RequireAuthorization();
    }

    private static async Task<AuctionView> GetAuction(Guid id, AuctionsDbContext db, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var auction = await db.Auctions.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw ProblemException.NotFound("Auction");
        return AuctionView.From(auction, clock.UtcNow, await YouAsync(db, auction, current.Id, ct));
    }

    private static async Task<AuctionView> GetAuctionByListing(Guid listingId, AuctionsDbContext db, ICurrentUser current, IClock clock, CancellationToken ct)
    {
        var auction = await db.Auctions.AsNoTracking().SingleOrDefaultAsync(a => a.ListingId == listingId, ct) ?? throw ProblemException.NotFound("Auction");
        return AuctionView.From(auction, clock.UtcNow, await YouAsync(db, auction, current.Id, ct));
    }

    /// <summary>Bid history with masked bidders and no maxima (feature design §4.5).</summary>
    private static async Task<List<BidHistoryItem>> GetBids(Guid id, AuctionsDbContext db, CancellationToken ct)
    {
        var bids = await db.Bids.AsNoTracking()
            .Where(b => b.AuctionId == id)
            .OrderByDescending(b => b.Seq)
            .Take(50)
            .ToListAsync(ct);
        return bids.Select(b => new BidHistoryItem(b.Seq, b.Amount, Handles.Mask(b.BidderHandle), b.Kind.ToString().ToLowerInvariant(), b.CreatedAt)).ToList();
    }

    private static Task<BidResponse> PlaceBid(
        Guid id, PlaceBidRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        BidService bids, ICurrentUser current, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            throw ProblemException.BadRequest("idempotency_key_required", "Send an Idempotency-Key header (up to 100 characters) with every bid.");
        return bids.PlaceBidAsync(id, current.RequireId(), current.Handle ?? "bidder", request.MaxAmount, idempotencyKey, ct);
    }

    private static Task<AuctionView> BuyNow(Guid id, BidService bids, ICurrentUser current, CancellationToken ct) =>
        bids.BuyNowAsync(id, current.RequireId(), current.Handle ?? "buyer", ct);

    /// <summary>The caller's auctions: winning, outbid, won or lost (system design §7).</summary>
    private static async Task<List<MyBiddingItem>> GetMyBidding(string? status, AuctionsDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var me = current.RequireId();
        var mine = await db.Bids.AsNoTracking()
            .Where(b => b.BidderId == me)
            .GroupBy(b => b.AuctionId)
            .Select(g => new { AuctionId = g.Key, YourMax = g.Max(b => b.MaxAmount) })
            .ToListAsync(ct);
        var ids = mine.Select(m => m.AuctionId).ToList();
        var auctions = await db.Auctions.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);

        var items = auctions.Select(a =>
        {
            var standing = a.Status switch
            {
                AuctionStatus.Live => a.LeaderId == me ? "winning" : "outbid",
                AuctionStatus.ClosedSold when a.WinnerId == me => "won",
                _ => "lost",
            };
            return new MyBiddingItem(a.Id, a.ListingId, a.Title, a.Currency, a.CurrentPrice,
                mine.Single(m => m.AuctionId == a.Id).YourMax, a.BidCount, a.EndsAt, AuctionView.StatusOf(a), standing);
        });

        if (!string.IsNullOrWhiteSpace(status))
            items = items.Where(i => i.Standing == status);
        return items.OrderBy(i => i.AuctionStatus == "live" ? 0 : 1).ThenBy(i => i.EndsAt).ToList();
    }

    private static async Task<YouView?> YouAsync(AuctionsDbContext db, Auction a, Guid? userId, CancellationToken ct)
    {
        if (userId is not { } me)
            return null;
        if (me == a.SellerId)
            return new YouView(IsSeller: true, HasBid: false, IsLeader: false, YourMax: null, Won: false);

        var yourMax = await db.Bids.Where(b => b.AuctionId == a.Id && b.BidderId == me).MaxAsync(b => (decimal?)b.MaxAmount, ct);
        var isLeader = a.LeaderId == me;
        return new YouView(false, yourMax is not null, isLeader, isLeader ? a.LeaderMax : yourMax, a.WinnerId == me);
    }
}
