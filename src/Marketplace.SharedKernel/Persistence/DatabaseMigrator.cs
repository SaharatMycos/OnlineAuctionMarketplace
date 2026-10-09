using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Marketplace.SharedKernel.Persistence;

/// <summary>
/// Applies pending EF Core migrations for every registered <see cref="ModuleDbContext"/>, in
/// registration order (<c>ModuleCatalog.All</c>). A Postgres advisory lock keeps concurrent starts
/// from racing. Only the api process calls this.
/// </summary>
public sealed class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    ILogger<DatabaseMigrator> logger)
{
    private const long LockKey = 7_302_517_640_001;

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using var lockConnection = await dataSource.OpenConnectionAsync(ct);
        await ExecuteAsync(lockConnection, $"SELECT pg_advisory_lock({LockKey})", ct);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            foreach (var context in scope.ServiceProvider.GetServices<ModuleDbContext>())
            {
                var pending = (await context.Database.GetPendingMigrationsAsync(ct)).ToList();
                if (pending.Count == 0)
                    continue;

                await context.Database.MigrateAsync(ct);
                logger.LogInformation("Migrated {Schema}: {Migrations}", context.Schema, string.Join(", ", pending));
            }
        }
        finally
        {
            await ExecuteAsync(lockConnection, $"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
