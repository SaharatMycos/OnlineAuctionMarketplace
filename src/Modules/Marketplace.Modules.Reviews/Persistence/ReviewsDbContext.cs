using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Reviews.Persistence;

/// <summary>
/// Code-first model of the "reviews" schema. Add DbSets here and IEntityTypeConfiguration classes
/// anywhere in this assembly, then: dotnet ef migrations add &lt;Name&gt; (see the skill's "Add a migration").
/// </summary>
internal sealed class ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => ReviewsModule.Schema;
}