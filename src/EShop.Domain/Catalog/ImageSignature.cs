namespace EShop.Domain.Catalog;

public sealed record ImageType(string ContentType, string Extension);

public static class ImageSignature
{
    /// <summary>Number of bytes needed to detect every supported format.</summary>
    public const int HeaderLength = 12;

    private static readonly ImageType Jpeg = new("image/jpeg", ".jpg");
    private static readonly ImageType Png = new("image/png", ".png");
    private static readonly ImageType WebP = new("image/webp", ".webp");

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Returns the image type for JPEG, PNG and WebP, or null for anything else.</summary>
    public static ImageType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegMagic))
        {
            return Jpeg;
        }

        if (header.StartsWith(PngMagic))
        {
            return Png;
        }

        if (header.Length >= HeaderLength
            && header[..4].SequenceEqual("RIFF"u8)
            && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        return null;
    }
}