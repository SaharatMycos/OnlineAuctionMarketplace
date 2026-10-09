using Marketplace.SharedKernel.Auth;
using Marketplace.SharedKernel.Events;
using Microsoft.AspNetCore.SignalR;

namespace Marketplace.Realtime;

/// <summary>
/// Anyone can watch an auction's live price, bid and end-time updates. Signed-in users (token in
/// <c>access_token</c>) also join their own <c>user:{id}</c> group for outbid/won/agreement events.
/// Updates come from outbox events relayed through Redis, never from client calls.
/// </summary>
public sealed class AuctionHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst(MarketplaceClaims.UserId)?.Value, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId));
        await base.OnConnectedAsync();
    }

    public Task Watch(Guid auctionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Auction(auctionId));

    public Task Unwatch(Guid auctionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Auction(auctionId));
}
