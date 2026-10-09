using Marketplace.Modules.Identity.Endpoints;
using Marketplace.Modules.Identity.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Identity;

/// <summary>Identity &amp; Orgs: users, verification, trust tiers, organisations, roles and approval limits (feature design §1).</summary>
public sealed class IdentityModule : IModule
{
    public const string Schema = "identity";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<IdentityDbContext>(Schema);

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => IdentityEndpoints.Map(endpoints);
}
