namespace EShop.Domain.Catalog;

public sealed record ProductData(
    Guid CategoryId, string Name, string Slug, string? Description, decimal Price,
    int? WarrantyMonths = null);