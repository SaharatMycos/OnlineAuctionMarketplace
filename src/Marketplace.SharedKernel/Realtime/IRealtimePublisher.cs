using Marketplace.SharedKernel.Events;

namespace Marketplace.SharedKernel.Realtime;

/// <summary>Hands a push to the realtime process (Redis in deployments). Called only by the outbox processor.</summary>
public interface IRealtimePublisher
{
    Task PublishAsync(RealtimePush push, CancellationToken ct);
}

/// <summary>Used where no realtime transport is configured (api, tests).</summary>
public sealed class NullRealtimePublisher : IRealtimePublisher
{
    public Task PublishAsync(RealtimePush push, CancellationToken ct) => Task.CompletedTask;
}
