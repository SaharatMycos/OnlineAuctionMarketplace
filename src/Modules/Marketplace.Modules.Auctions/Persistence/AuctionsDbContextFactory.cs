using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Auctions.Persistence;

/// <summary>Lets dotnet ef build <see cref="AuctionsDbContext"/> at design time.</summary>
internal sealed class AuctionsDbContextFactory : ModuleDesignTimeFactory<AuctionsDbContext>
{
    protected override string Schema => AuctionsModule.Schema;
}