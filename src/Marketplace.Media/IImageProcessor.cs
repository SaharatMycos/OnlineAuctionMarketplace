namespace Marketplace.Media;

public interface IImageProcessor
{
    Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken cancellationToken = default);
}

public sealed record ProcessedImage(byte[] Content, string ContentType, int Width, int Height);

public enum ImageFailure { Unsupported, Corrupt, TooLarge, Animated }

public sealed class ImageProcessingException(ImageFailure failure, string message) : Exception(message)
{
    public ImageFailure Failure { get; } = failure;
}

public static class ImageLimits
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const long MaxPixels = 40_000_000;
    public const int MaxEdge = 2048;
}
