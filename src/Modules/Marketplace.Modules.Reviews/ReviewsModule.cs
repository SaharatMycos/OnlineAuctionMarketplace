using Marketplace.Modules.Reviews.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Reviews;

/// <summary>Reviews &amp; Reports: ratings, reviews and problem reports (feature design §8).</summary>
public sealed class ReviewsModule : IModule
{
    public const string Schema = "reviews";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<ReviewsDbContext>(Schema);
}