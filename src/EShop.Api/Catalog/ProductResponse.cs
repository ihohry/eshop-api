namespace EShop.Api.Catalog;

public sealed record ProductResponse(
    Guid Id,
    Guid CategoryId,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    string Currency,
    bool InStock,
    int WarrantyMonths,
    List<ProductImageResponse> Images);