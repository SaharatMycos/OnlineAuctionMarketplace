using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Marketplace.SharedKernel.Persistence;

public static class PersistenceExtensions
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Registers a module's DbContext on the shared data source, and exposes it as <see cref="ModuleDbContext"/> for the migrator and the outbox relay.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((sp, options) =>
            options.UseModuleDatabase(sp.GetRequiredService<NpgsqlDataSource>(), schema));
        services.AddScoped<ModuleDbContext>(sp => sp.GetRequiredService<TContext>());
        return services;
    }

    /// <summary>Runtime: Npgsql on the shared, pooled data source.</summary>
    public static DbContextOptionsBuilder UseModuleDatabase(this DbContextOptionsBuilder builder, NpgsqlDataSource dataSource, string schema) =>
        builder.UseNpgsql(dataSource, npgsql => npgsql.ForModule(schema)).UseSnakeCaseNamingConvention();

    /// <summary>Design time (dotnet ef) and tests: Npgsql from a connection string.</summary>
    public static DbContextOptionsBuilder UseModuleDatabase(this DbContextOptionsBuilder builder, string connectionString, string schema) =>
        builder.UseNpgsql(connectionString, npgsql => npgsql.ForModule(schema)).UseSnakeCaseNamingConvention();

    private static void ForModule(this NpgsqlDbContextOptionsBuilder npgsql, string schema)
    {
        npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema);
        npgsql.UseNetTopologySuite();
    }
}
