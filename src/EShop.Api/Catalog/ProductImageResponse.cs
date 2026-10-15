using EShop.Domain.Catalog;

namespace EShop.Api.Catalog;

public sealed record ProductImageResponse(Guid Id, string Url, string? AltText, int Position)
{
    public static ProductImageResponse From(ProductImage image)
        => new(image.Id, $"/images/{image.FileName}", image.AltText, image.Position);
}