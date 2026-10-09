using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Reviews.Persistence;

/// <summary>Lets dotnet ef build <see cref="ReviewsDbContext"/> at design time.</summary>
internal sealed class ReviewsDbContextFactory : ModuleDesignTimeFactory<ReviewsDbContext>
{
    protected override string Schema => ReviewsModule.Schema;
}