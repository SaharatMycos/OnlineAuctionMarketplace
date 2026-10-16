using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.Media;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddMarketplaceMedia(this IServiceCollection services)
    {
        services.TryAddSingleton<IImageProcessor, SkiaImageProcessor>();
        return services;
    }
}
