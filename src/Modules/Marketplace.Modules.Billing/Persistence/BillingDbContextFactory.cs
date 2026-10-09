using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Billing.Persistence;

/// <summary>Lets dotnet ef build <see cref="BillingDbContext"/> at design time.</summary>
internal sealed class BillingDbContextFactory : ModuleDesignTimeFactory<BillingDbContext>
{
    protected override string Schema => BillingModule.Schema;
}