namespace EShop.Domain.Catalog;

public interface ICatalogAdminService
{
    Task<Category> CreateCategoryAsync(string name, string slug, Guid? parentId, CancellationToken ct);
    Task<Category> UpdateCategoryAsync(Guid id, string name, string slug, CancellationToken ct);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct);

    Task<Product> CreateProductAsync(ProductData data, CancellationToken ct);

    Task<Product> UpdateProductAsync(Guid id, ProductData data, bool isActive, CancellationToken ct);

    Task DeactivateProductAsync(Guid id, CancellationToken ct);
}