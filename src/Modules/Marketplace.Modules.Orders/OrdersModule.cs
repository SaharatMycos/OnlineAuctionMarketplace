using System.Runtime.CompilerServices;
using Marketplace.Modules.Auctions.Contracts;
using Marketplace.Modules.Orders.Application;
using Marketplace.Modules.Orders.Endpoints;
using Marketplace.Modules.Orders.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("Marketplace.UnitTests")]

namespace Marketplace.Modules.Orders;

/// <summary>Orders &amp; Deals: orders, deal agreements, paid/received confirmations (feature design §6).</summary>
public sealed class OrdersModule : IModule
{
    public const string Schema = "orders";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersDbContext>(Schema);
        services.AddScoped<OrderService>();
        services.AddIntegrationEventHandler<AuctionClosed, CreateOrderOnAuctionClosed>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => OrderEndpoints.Map(endpoints);
}
