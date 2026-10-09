using System.Text.Json;
using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Auctions.Domain;
using Marketplace.Modules.Auctions.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Auctions.Application;

/// <summary>
/// The bid path (system design §3): one transaction, auction row locked, rules applied by the
/// <see cref="Auction"/> aggregate, bids + outbox events + idempotency record saved together.
/// </summary>
internal sealed class BidService(AuctionsDbContext db, IClock clock)
{
    public async Task<BidResponse> PlaceBidAsync(Guid auctionId, Guid bidderId, string bidderHandle, decimal maxAmount, string idempotencyKey, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var auction = await db.LockAuctionAsync(auctionId, ct) ?? throw ProblemException.NotFound("Auction");

        // Checked under the lock, so two concurrent retries with the same key can't both bid.
        var previous = await db.BidRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.BidderId == bidderId && r.IdempotencyKey == idempotencyKey, ct);
        if (previous is not null)
        {
            if (previous.AuctionId != auctionId)
                throw ProblemException.Conflict("idempotency_key_reused", "This Idempotency-Key was already used for another auction.");
            return JsonSerializer.Deserialize<BidResponse>(previous.Response, EventJson.Options)!;
        }

        var now = clock.UtcNow;
        var outcome = auction.PlaceBid(bidderId, bidderHandle, maxAmount, now);
        db.Bids.AddRange(outcome.Bids);
        db.Publish(new BidPlaced(
            auction.Id, auction.ListingId, auction.Title, auction.LastSeq, auction.CurrentPrice, auction.BidCount,
            auction.EndsAt, outcome.Extended, auction.BuyNowAvailable, Handles.Mask(auction.LeaderHandle!), outcome.OutbidUserId), now);

        var response = new BidResponse(
            auction.Id, auction.LeaderId == bidderId, auction.CurrentPrice, auction.MinNextBid, auction.BidCount,
            auction.EndsAt, maxAmount, outcome.Extended, auction.ReserveMet, AuctionView.StatusOf(auction));
        db.BidRequests.Add(new BidRequest
        {
            BidderId = bidderId,
            IdempotencyKey = idempotencyKey,
            AuctionId = auction.Id,
            Response = JsonSerializer.Serialize(response, EventJson.Options),
            CreatedAt = now,
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return response;
    }

    public async Task<AuctionView> BuyNowAsync(Guid auctionId, Guid buyerId, string buyerHandle, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var auction = await db.LockAuctionAsync(auctionId, ct) ?? throw ProblemException.NotFound("Auction");

        var now = clock.UtcNow;
        db.Bids.Add(auction.BuyNow(buyerId, buyerHandle, now));
        db.Publish(Closed(auction), now);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return AuctionView.From(auction, now, new YouView(false, true, true, auction.FinalPrice, Won: true));
    }

    public static AuctionClosed Closed(Auction a) => new(
        a.Id, a.ListingId, a.Title, a.SellerId, a.SellerHandle,
        a.Status == AuctionStatus.ClosedSold, a.WinnerId, a.WinnerId is null ? null : a.LeaderHandle,
        a.FinalPrice, a.Currency, a.CloseReason ?? "ended", a.ClosedAt!.Value, a.LastSeq);
}
