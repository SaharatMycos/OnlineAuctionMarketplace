using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.SharedKernel.Jobs;

/// <summary>
/// Periodic work owned by a module (e.g. closing auctions). Modules register jobs in AddServices;
/// only the worker process runs them. Tests call <see cref="RunOnceAsync"/> directly.
/// </summary>
public interface IBackgroundJob
{
    /// <summary>Stable name, e.g. <c>auctions.close</c>.</summary>
    string Name { get; }
    TimeSpan Interval { get; }
    /// <summary>Does one pass and returns how many items it handled. Must be idempotent and safe to run in parallel.</summary>
    Task<int> RunOnceAsync(CancellationToken ct);
}

public static class BackgroundJobServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJob<TJob>(this IServiceCollection services) where TJob : class, IBackgroundJob
    {
        services.AddSingleton<IBackgroundJob, TJob>();
        return services;
    }
}
