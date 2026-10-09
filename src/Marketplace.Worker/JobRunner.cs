using Marketplace.SharedKernel.Jobs;

namespace Marketplace.Worker;

/// <summary>Runs every module's <see cref="IBackgroundJob"/> on its own interval (e.g. <c>auctions.close</c> every second).</summary>
public sealed class JobRunner(IEnumerable<IBackgroundJob> jobs, ILogger<JobRunner> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(jobs.Select(job => RunAsync(job, stoppingToken)));

    private async Task RunAsync(IBackgroundJob job, CancellationToken stoppingToken)
    {
        logger.LogInformation("Job {Job} started (every {Interval})", job.Name, job.Interval);
        using var timer = new PeriodicTimer(job.Interval);
        do
        {
            try
            {
                var handled = await job.RunOnceAsync(stoppingToken);
                if (handled > 0)
                    logger.LogInformation("Job {Job} handled {Count} item(s)", job.Name, handled);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job {Job} failed", job.Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
