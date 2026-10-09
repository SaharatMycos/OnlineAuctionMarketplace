using Marketplace.Modules.Billing.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Billing;

/// <summary>Fees &amp; Billing: seller fees and monthly fee invoices (feature design §6.4).</summary>
public sealed class BillingModule : IModule
{
    public const string Schema = "billing";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<BillingDbContext>(Schema);
}