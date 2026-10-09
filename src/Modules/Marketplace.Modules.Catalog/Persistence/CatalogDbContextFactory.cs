using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Catalog.Persistence;

/// <summary>Lets dotnet ef build <see cref="CatalogDbContext"/> at design time.</summary>
internal sealed class CatalogDbContextFactory : ModuleDesignTimeFactory<CatalogDbContext>
{
    protected override string Schema => CatalogModule.Schema;
}