using Marketplace.SharedKernel.Outbox;

namespace Marketplace.Worker;

/// <summary>
/// Relays every module's outbox (system design §2): runs integration event handlers and pushes
/// realtime events to Redis. Polls quickly while there's work and backs off when idle.
/// </summary>
public sealed class OutboxRelay(OutboxProcessor processor, ILogger<OutboxRelay> logger) : BackgroundService
{
    private static readonly TimeSpan BusyDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox relay started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox relay pass failed");
            }

            await Task.Delay(processed > 0 ? BusyDelay : IdleDelay, stoppingToken);
        }
    }
}
