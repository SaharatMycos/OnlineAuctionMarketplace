using Marketplace.Modules.Location.Application;
using Marketplace.Modules.Location.Contracts;
using Marketplace.Modules.Location.Endpoints;
using Marketplace.Modules.Location.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Location;

/// <summary>Location: buyer location, item locations, fuzzed public points, geocoding (feature design §9, system design §4).</summary>
public sealed class LocationModule : IModule
{
    public const string Schema = "location";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LocationDbContext>(Schema);
        services.AddScoped<LocationService>();
        services.AddScoped<ILocationService>(sp => sp.GetRequiredService<LocationService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => LocationEndpoints.Map(endpoints);
}
