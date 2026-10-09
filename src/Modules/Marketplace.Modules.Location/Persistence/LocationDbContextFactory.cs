using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Location.Persistence;

/// <summary>Lets dotnet ef build <see cref="LocationDbContext"/> at design time.</summary>
internal sealed class LocationDbContextFactory : ModuleDesignTimeFactory<LocationDbContext>
{
    protected override string Schema => LocationModule.Schema;
}