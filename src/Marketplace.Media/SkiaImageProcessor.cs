using System.Runtime.InteropServices;
using SkiaSharp;

namespace Marketplace.Media;

internal sealed class SkiaImageProcessor : IImageProcessor
{
    public async Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int count;
        while ((count = await input.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > ImageLimits.MaxBytes)
                throw new ImageProcessingException(ImageFailure.TooLarge, "Photos must be at most 10 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Wrap the pinned buffer instead of copying it; disposed after the codec and data that read it.
        using var pin = new PinnedArray(buffer.GetBuffer());
        using var data = SKData.Create(pin.Address, (int)buffer.Length);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            throw new ImageProcessingException(ImageFailure.Corrupt, "The file is not a readable image.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new ImageProcessingException(ImageFailure.Unsupported, "Use JPEG, PNG or WebP photos.");
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > ImageLimits.MaxPixels)
            throw new ImageProcessingException(ImageFailure.TooLarge, "Photos must be at most 40 megapixels.");
        if (codec.FrameCount > 1 || HasAnimatedPng(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            throw new ImageProcessingException(ImageFailure.Animated, "Animated photos are not supported.");

        // Let codecs that support it (JPEG, WebP) downsample while decoding, keeping at least MaxEdge pixels.
        var size = codec.Info.Size;
        var longest = Math.Max(size.Width, size.Height);
        if (longest > ImageLimits.MaxEdge)
        {
            var scaled = codec.GetScaledDimensions((float)ImageLimits.MaxEdge / longest);
            if (Math.Max(scaled.Width, scaled.Height) >= ImageLimits.MaxEdge) size = scaled;
        }
        using var decoded = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new ImageProcessingException(ImageFailure.Corrupt, "The image is incomplete or corrupt.");
        cancellationToken.ThrowIfCancellationRequested();
        var origin = codec.EncodedOrigin;
        var rotated = (int)origin >= 5;
        var width = rotated ? decoded.Height : decoded.Width;
        var height = rotated ? decoded.Width : decoded.Height;
        var scale = Math.Min(1d, (double)ImageLimits.MaxEdge / Math.Max(width, height));
        var outWidth = Math.Max(1, (int)Math.Round(width * scale));
        var outHeight = Math.Max(1, (int)Math.Round(height * scale));
        using var output = new SKBitmap(new SKImageInfo(outWidth, outHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(output))
        {
            canvas.Clear(SKColors.White);
            canvas.Scale((float)outWidth / width, (float)outHeight / height);
            // EXIF transforms map source pixels into an upright, positive-coordinate rectangle.
            var matrix = origin switch
            {
                SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new SKMatrix(0, -1, width, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, width, -1, 0, height, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, height, 0, 0, 1),
                _ => SKMatrix.Identity,
            };
            canvas.Concat(matrix);
            using var image = SKImage.FromBitmap(decoded);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }
        using var encoded = output.Encode(SKEncodedImageFormat.Jpeg, 85);
        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessedImage(encoded.ToArray(), "image/jpeg", outWidth, outHeight);
    }

    // Skia may decode APNG as a still PNG; inspect chunk types as well as codec frame count.
    private static bool HasAnimatedPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8 || !bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (bytes.Slice(offset + 4, 4).SequenceEqual("acTL"u8)) return true;
            if (length > bytes.Length - offset - 12) break;
            offset += checked((int)length + 12);
        }
        return false;
    }

    private sealed class PinnedArray(byte[] array) : IDisposable
    {
        private GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        public IntPtr Address => handle.AddrOfPinnedObject();
        public void Dispose() => handle.Free();
    }
}
