using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Search.Persistence;

/// <summary>Lets dotnet ef build <see cref="SearchDbContext"/> at design time.</summary>
internal sealed class SearchDbContextFactory : ModuleDesignTimeFactory<SearchDbContext>
{
    protected override string Schema => SearchModule.Schema;
}