using System.Buffers.Binary;
using SkiaSharp;

namespace Marketplace.Media.Tests;

public sealed class ImageProcessorTests
{
    private static IImageProcessor Processor() => new SkiaImageProcessor();

    private static byte[] Image(SKEncodedImageFormat format, int width = 20, int height = 10, bool transparent = false)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(transparent ? SKColors.Transparent : SKColors.Red);
        using var data = bitmap.Encode(format, 100);
        return data.ToArray();
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task Supported_formats_become_jpeg_without_upscaling(SKEncodedImageFormat format)
    {
        var result = await Processor().ProcessAsync(new MemoryStream(Image(format)));
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal((20, 10), (result.Width, result.Height));
        using var image = SKBitmap.Decode(result.Content);
        Assert.Equal(20, image.Width);
        Assert.True(image.GetPixel(10, 5).Red > 240);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task Large_scalable_image_is_downsampled_to_exact_max_edge(SKEncodedImageFormat format)
    {
        var result = await Processor().ProcessAsync(new MemoryStream(Image(format, 5000, 2500)));
        Assert.Equal((2048, 1024), (result.Width, result.Height));
        using var image = SKBitmap.Decode(result.Content);
        Assert.True(image.GetPixel(1024, 512).Red > 240);
    }

    [Fact]
    public async Task Large_image_is_resized_and_transparency_becomes_white()
    {
        var result = await Processor().ProcessAsync(new MemoryStream(Image(SKEncodedImageFormat.Png, 3000, 1500, true)));
        Assert.Equal((2048, 1024), (result.Width, result.Height));
        using var image = SKBitmap.Decode(result.Content);
        var pixel = image.GetPixel(100, 100);
        Assert.True(pixel.Red > 250 && pixel.Green > 250 && pixel.Blue > 250);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public async Task Orientation_is_applied_and_exif_is_discarded(int orientation, bool rotated)
    {
        using var bitmap = new SKBitmap(40, 20);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
            using var paint = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(20, 0, 20, 20, paint);
        }
        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        var original = jpeg.ToArray();
        // APP1 segment containing a little-endian TIFF orientation tag.
        byte[] exif = [0xff, 0xe1, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0, 0, 0, 0, 0];
        var input = original[..2].Concat(exif).Concat(original[2..]).ToArray();
        var result = await Processor().ProcessAsync(new MemoryStream(input));
        Assert.Equal(rotated ? (20, 40) : (40, 20), (result.Width, result.Height));
        Assert.DoesNotContain("Exif", System.Text.Encoding.Latin1.GetString(result.Content));
        using var image = SKBitmap.Decode(result.Content);
        var first = image.GetPixel(3, 3);
        var shouldBeBlue = orientation is 2 or 3 or 7 or 8;
        Assert.True(shouldBeBlue ? first.Blue > 200 : first.Red > 200);
    }

    [Fact]
    public async Task Corrupt_unsupported_and_truncated_images_are_rejected()
    {
        var corrupt = await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream([1, 2, 3])));
        Assert.Equal(ImageFailure.Corrupt, corrupt.Failure);
        var unsupported = await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream(Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"))));
        Assert.Equal(ImageFailure.Unsupported, unsupported.Failure);
        var jpeg = Image(SKEncodedImageFormat.Jpeg, 100, 100);
        await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream(jpeg[..(jpeg.Length / 2)])));
    }

    [Fact]
    public async Task Byte_and_pixel_limits_are_checked_before_decoding()
    {
        var oversized = await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream(new byte[ImageLimits.MaxBytes + 1])));
        Assert.Equal(ImageFailure.TooLarge, oversized.Failure);
        var png = Image(SKEncodedImageFormat.Png);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 10000);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), 10000);
        WriteCrc(png.AsSpan(12, 17), png.AsSpan(29, 4));
        var dimensions = await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream(png)));
        Assert.Equal(ImageFailure.TooLarge, dimensions.Failure);
    }

    [Fact]
    public async Task Animated_gif_and_apng_are_rejected()
    {
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream(gif)));
        var png = Image(SKEncodedImageFormat.Png);
        byte[] chunk = [0, 0, 0, 8, 97, 99, 84, 76, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0];
        WriteCrc(chunk.AsSpan(4, 12), chunk.AsSpan(16, 4));
        var apng = png[..33].Concat(chunk).Concat(png[33..]).ToArray();
        var error = await Assert.ThrowsAsync<ImageProcessingException>(() => Processor().ProcessAsync(new MemoryStream(apng)));
        Assert.Equal(ImageFailure.Animated, error.Failure);
    }

    [Fact]
    public async Task Cancellation_is_observed()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor().ProcessAsync(new MemoryStream(Image(SKEncodedImageFormat.Png)), cancellation.Token));
    }

    private static void WriteCrc(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        uint crc = 0xffffffff;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0);
        }
        BinaryPrimitives.WriteUInt32BigEndian(destination, ~crc);
    }
}
