using Marketplace.Modules.Auctions.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Auctions.Application;

/// <summary>
/// Closes live auctions whose (possibly extended) end time has passed (system design §3). Each pass
/// claims due auctions with <c>FOR UPDATE SKIP LOCKED</c> so it never fights the bid path or another
/// worker; <see cref="Domain.Auction.Close"/> re-checks the end time under the lock and is idempotent.
/// Running every second also makes it the sweeper for anything missed.
/// </summary>
internal sealed class AuctionCloser(IServiceScopeFactory scopeFactory, IClock clock) : IBackgroundJob
{
    public string Name => "auctions.close";
    public TimeSpan Interval => TimeSpan.FromSeconds(1);

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuctionsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var now = clock.UtcNow;
        var due = await db.Auctions
            .FromSql($"""
                SELECT * FROM auctions.auction
                WHERE status = 'Live' AND ends_at <= {now}
                ORDER BY ends_at
                LIMIT 50
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        var closed = 0;
        foreach (var auction in due)
        {
            if (!auction.Close(now))
                continue;
            db.Publish(BidService.Closed(auction), now);
            closed++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return closed;
    }
}
