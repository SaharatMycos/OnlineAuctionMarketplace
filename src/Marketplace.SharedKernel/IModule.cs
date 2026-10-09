using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.SharedKernel;

/// <summary>
/// A module of the modular monolith (system design §2). A module owns one Postgres schema,
/// named after <see cref="Name"/>, modelled code-first by its <c>ModuleDbContext</c> with EF Core
/// migrations. Other modules talk to it only through its public contracts and outbox events,
/// never through its internals.
/// </summary>
public interface IModule
{
    /// <summary>Lowercase module name. Also the module's Postgres schema.</summary>
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration) { }

    void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
