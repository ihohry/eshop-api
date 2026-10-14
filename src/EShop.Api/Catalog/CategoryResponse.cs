namespace EShop.Api.Catalog;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<CategoryResponse> Children);