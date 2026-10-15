using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Marketplace.SharedKernel.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create a module's DbContext without starting a host. Each module declares
/// one: <c>internal sealed class XDbContextFactory : ModuleDesignTimeFactory&lt;XDbContext&gt;</c>.
/// Reads <c>ConnectionStrings:Postgres</c> like the api does: the startup project's
/// <c>appsettings.json</c>, then <c>appsettings.Development.json</c>, then environment variables.
/// </summary>
public abstract class ModuleDesignTimeFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : ModuleDbContext
{
    protected abstract string Schema { get; }

    public TContext CreateDbContext(string[] args)
    {
        // dotnet ef may run from the shell's folder; the startup project's appsettings are copied next to its assembly.
        var basePath = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            .FirstOrDefault(dir => File.Exists(Path.Combine(dir, "appsettings.json"))) ?? AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres") is { Length: > 0 } cs
            ? cs
            : throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. Run scripts/init-env.ps1 to create src/Marketplace.Api/appsettings.Development.json, or set ConnectionStrings__Postgres.");
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseModuleDatabase(connectionString, Schema);
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }
}
