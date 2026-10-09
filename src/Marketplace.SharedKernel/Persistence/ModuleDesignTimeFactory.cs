using Marketplace.SharedKernel.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Marketplace.SharedKernel.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create a module's DbContext without starting a host. Each module declares
/// one: <c>internal sealed class XDbContextFactory : ModuleDesignTimeFactory&lt;XDbContext&gt;</c>.
/// Uses <c>ConnectionStrings__Postgres</c> from the environment or the repository-root <c>.env</c>.
/// </summary>
public abstract class ModuleDesignTimeFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : ModuleDbContext
{
    protected abstract string Schema { get; }

    public TContext CreateDbContext(string[] args)
    {
        DotEnv.Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Postgres is not set. Run scripts/init-env.ps1 to create .env, or set the variable.");
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseModuleDatabase(connectionString, Schema);
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }
}
