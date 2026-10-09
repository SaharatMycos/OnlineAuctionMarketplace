using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Admin.Persistence;

/// <summary>Lets dotnet ef build <see cref="AdminDbContext"/> at design time.</summary>
internal sealed class AdminDbContextFactory : ModuleDesignTimeFactory<AdminDbContext>
{
    protected override string Schema => AdminModule.Schema;
}