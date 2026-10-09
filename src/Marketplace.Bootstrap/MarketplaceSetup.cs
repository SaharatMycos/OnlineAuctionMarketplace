using System.Reflection;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Outbox;
using Marketplace.SharedKernel.Persistence;
using Marketplace.SharedKernel.Realtime;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Marketplace.Bootstrap;

public static class MarketplaceSetup
{
    /// <summary>Registers platform services and every module (including its DbContext and event handlers). Used by all three hosts.</summary>
    public static IServiceCollection AddMarketplace(this IServiceCollection services, IConfiguration configuration)
    {
        // One pooled data source shared by every module DbContext. Resolved lazily so hosts and
        // tests can start without a database.
        services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseNetTopologySuite();
            return builder.Build();
        });
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<DatabaseMigrator>();

        services.AddSingleton(new IntegrationEventRegistry(EventAssemblies()));
        services.AddSingleton<OutboxProcessor>();
        services.TryAddSingleton<IRealtimePublisher, NullRealtimePublisher>();

        foreach (var module in ModuleCatalog.All)
            module.AddServices(services, configuration);

        return services;
    }

    public static IEndpointRouteBuilder MapMarketplaceModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in ModuleCatalog.All)
            module.MapEndpoints(endpoints);

        return endpoints;
    }

    /// <summary>Applies pending code-first migrations for every module, in <see cref="ModuleCatalog.All"/> order.</summary>
    public static Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default) =>
        services.GetRequiredService<DatabaseMigrator>().MigrateAsync(ct);

    /// <summary>Module assemblies plus the <c>*.Contracts</c> assemblies they reference, where integration events live.</summary>
    public static IReadOnlyList<Assembly> EventAssemblies()
    {
        var modules = ModuleCatalog.All.Select(m => m.GetType().Assembly).ToList();
        var contracts = modules
            .SelectMany(a => a.GetReferencedAssemblies())
            .Where(n => n.Name!.StartsWith("Marketplace.Modules.", StringComparison.Ordinal) && n.Name.EndsWith(".Contracts", StringComparison.Ordinal))
            .Select(Assembly.Load);
        return [.. modules, .. contracts];
    }
}
