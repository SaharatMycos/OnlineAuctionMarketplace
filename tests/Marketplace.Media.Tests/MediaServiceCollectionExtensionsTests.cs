using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Media.Tests;

public sealed class MediaServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMarketplaceMedia_registers_image_processor_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddMarketplaceMedia();
        using var provider = services.BuildServiceProvider();

        var processor = provider.GetRequiredService<IImageProcessor>();

        Assert.IsType<SkiaImageProcessor>(processor);
        Assert.Same(processor, provider.GetRequiredService<IImageProcessor>());
    }
}
