using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Billing.Persistence;

/// <summary>
/// Code-first model of the "billing" schema. Add DbSets here and IEntityTypeConfiguration classes
/// anywhere in this assembly, then: dotnet ef migrations add &lt;Name&gt; (see the skill's "Add a migration").
/// </summary>
internal sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => BillingModule.Schema;
}