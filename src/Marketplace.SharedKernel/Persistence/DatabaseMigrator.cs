using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Marketplace.SharedKernel.Persistence;

/// <summary>
/// Creates the database if needed, then applies pending EF Core migrations for every registered <see cref="ModuleDbContext"/>, in
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
        await EnsureDatabaseAsync(ct);

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

    /// <summary>
    /// Code-first all the way down: creates the database itself when it doesn't exist yet (EF connects
    /// to the server's maintenance database to run CREATE DATABASE). The migrations then add PostGIS,
    /// so the configured user must be allowed to create databases and the postgis extension.
    /// </summary>
    private async Task EnsureDatabaseAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetServices<ModuleDbContext>().First();
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync(ct))
            return;

        try
        {
            await creator.CreateAsync(ct);
            logger.LogInformation("Created database {Database}", context.Database.GetDbConnection().Database);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // Another api instance created it first.
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
