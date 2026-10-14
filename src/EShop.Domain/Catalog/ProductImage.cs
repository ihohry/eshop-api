namespace EShop.Domain.Catalog;

public sealed class ProductImage
{
    private ProductImage()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public int Position { get; private set; }
    public string? AltText { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProductImage Create(
        Guid productId,
        string fileName,
        string contentType,
        long sizeBytes,
        int position,
        string? altText,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        var alt = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        if (alt is { Length: > ImageDefaults.AltTextMaxLength })
        {
            throw new ArgumentException(
                $"Alt text must be at most {ImageDefaults.AltTextMaxLength} characters.",
                nameof(altText));
        }

        return new ProductImage
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Position = position,
            AltText = alt,
            CreatedAt = createdAt,
        };
    }
}