using System.Runtime.CompilerServices;
using Marketplace.Modules.Auctions.Application;
using Marketplace.Modules.Auctions.Endpoints;
using Marketplace.Modules.Auctions.Persistence;
using Marketplace.Modules.Catalog.Contracts;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Jobs;
using Marketplace.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("Marketplace.UnitTests")]

namespace Marketplace.Modules.Auctions;

/// <summary>Auctions &amp; Bidding: formats, proxy bidding, soft close, auction close (feature design §3-§5, system design §3).</summary>
public sealed class AuctionsModule : IModule
{
    public const string Schema = "auctions";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AuctionsDbContext>(Schema);
        services.AddScoped<BidService>();
        services.AddIntegrationEventHandler<ListingPublished, StartAuctionOnListingPublished>();
        services.AddBackgroundJob<AuctionCloser>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => AuctionEndpoints.Map(endpoints);
}
