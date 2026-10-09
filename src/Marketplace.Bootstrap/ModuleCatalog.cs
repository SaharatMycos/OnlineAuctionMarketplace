using Marketplace.Modules.Admin;
using Marketplace.Modules.Auctions;
using Marketplace.Modules.Billing;
using Marketplace.Modules.Catalog;
using Marketplace.Modules.Identity;
using Marketplace.Modules.Location;
using Marketplace.Modules.Messaging;
using Marketplace.Modules.Notifications;
using Marketplace.Modules.Orders;
using Marketplace.Modules.Reviews;
using Marketplace.Modules.Search;
using Marketplace.SharedKernel;

namespace Marketplace.Bootstrap;

/// <summary>The one place that knows every module. Hosts compose the app from this list.</summary>
public static class ModuleCatalog
{
    public static readonly IReadOnlyList<IModule> All =
    [
        new IdentityModule(),
        new LocationModule(),
        new CatalogModule(),
        new AuctionsModule(),
        new OrdersModule(),
        new BillingModule(),
        new ReviewsModule(),
        new MessagingModule(),
        new NotificationsModule(),
        new SearchModule(),
        new AdminModule(),
    ];
}
