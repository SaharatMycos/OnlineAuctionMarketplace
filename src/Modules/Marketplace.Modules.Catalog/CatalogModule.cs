using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Catalog.Application;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Catalog.Endpoints;
using Marketplace.Modules.Catalog.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Catalog;

/// <summary>Catalog &amp; Listings: categories, attributes, listings, photos, delivery options (feature design §2).</summary>
public sealed class CatalogModule : IModule
{
    public const string Schema = "catalog";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CatalogDbContext>(Schema);
        services.AddScoped<IListingQueries, ListingQueries>();
        services.AddIntegrationEventHandler<AuctionClosed, EndListingOnAuctionClosed>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => CatalogEndpoints.Map(endpoints);
}
