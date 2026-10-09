using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Identity.Persistence;

/// <summary>Lets dotnet ef build <see cref="IdentityDbContext"/> at design time.</summary>
internal sealed class IdentityDbContextFactory : ModuleDesignTimeFactory<IdentityDbContext>
{
    protected override string Schema => IdentityModule.Schema;
}