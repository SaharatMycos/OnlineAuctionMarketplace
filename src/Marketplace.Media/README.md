# Marketplace.Media

Reusable image processing without HTTP, database or marketplace-domain dependencies. Register AddMarketplaceMedia() and inject IImageProcessor. ProcessAsync accepts a readable stream and cancellation token, returning ProcessedImage (Content, ContentType, Width, Height). ImageProcessingException exposes an ImageFailure value for invalid input.

The internal SkiaSharp engine accepts static JPEG, PNG and WebP images, at most 10 MiB and 40 megapixels. It applies EXIF orientation, shrinks to a maximum 2,048-pixel edge, flattens onto white and returns a fresh JPEG at quality 85. Original metadata is discarded. Streams remain owned by callers. Cancellation is checked during reads and between synchronous native processing steps.

Consumers use only our interface and DTOs. Replace the registered implementation to change engines; retain the normalization contract and run Marketplace.Media.Tests. Native Linux assets and third-party license notices are copied into published output.
