using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.Modules.Search.Application;
using Marketplace.Modules.Search.Endpoints;
using Marketplace.Modules.Search.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Search;

/// <summary>Search: listing index, geo search and saved searches (feature design §9, system design §4).</summary>
public sealed class SearchModule : IModule
{
    public const string Schema = "search";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SearchDbContext>(Schema);
        services.AddIntegrationEventHandler<ListingPublished, AddCardOnListingPublished>();
        services.AddIntegrationEventHandler<AuctionStarted, LinkCardOnAuctionStarted>();
        services.AddIntegrationEventHandler<BidPlaced, UpdateCardOnBidPlaced>();
        services.AddIntegrationEventHandler<AuctionClosed, UpdateCardOnAuctionClosed>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => SearchEndpoints.Map(endpoints);
}
