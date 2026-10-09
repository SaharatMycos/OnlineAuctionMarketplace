using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Orders.Persistence;

/// <summary>Lets dotnet ef build <see cref="OrdersDbContext"/> at design time.</summary>
internal sealed class OrdersDbContextFactory : ModuleDesignTimeFactory<OrdersDbContext>
{
    protected override string Schema => OrdersModule.Schema;
}