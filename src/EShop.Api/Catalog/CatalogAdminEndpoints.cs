using EShop.Api.Auth;
using EShop.Domain.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EShop.Api.Catalog;

public static class CatalogAdminEndpoints
{
    public static IEndpointRouteBuilder MapCatalogAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapPost("/categories", CreateCategory);
        group.MapPut("/categories/{id:guid}", UpdateCategory);
        group.MapDelete("/categories/{id:guid}", DeleteCategory);

        group.MapPost("/products", CreateProduct);
        group.MapPut("/products/{id:guid}", UpdateProduct);
        group.MapDelete("/products/{id:guid}", DeactivateProduct);

        return app;
    }

    private static async Task<Created<AdminCategoryResponse>> CreateCategory(
        CategoryCreateRequest request, ICatalogAdminService service, CancellationToken ct)
    {
        var category = await service.CreateCategoryAsync(request.Name, request.Slug, request.ParentId, ct);
        return TypedResults.Created($"/admin/categories/{category.Id}", ToResponse(category));
    }

    private static async Task<Ok<AdminCategoryResponse>> UpdateCategory(
        Guid id, CategoryUpdateRequest request, ICatalogAdminService service, CancellationToken ct)
    {
        var category = await service.UpdateCategoryAsync(id, request.Name, request.Slug, ct);
        return TypedResults.Ok(ToResponse(category));
    }

    private static async Task<NoContent> DeleteCategory(
        Guid id, ICatalogAdminService service, CancellationToken ct)
    {
        await service.DeleteCategoryAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<AdminProductResponse>> CreateProduct(
        ProductCreateRequest request, ICatalogAdminService service, CancellationToken ct)
    {
        var product = await service.CreateProductAsync(
            new ProductData(request.CategoryId, request.Name, request.Slug, request.Description, request.Price),
            ct);
        return TypedResults.Created($"/products/{product.Slug}", ToResponse(product));
    }

    private static async Task<Ok<AdminProductResponse>> UpdateProduct(
        Guid id, ProductUpdateRequest request, ICatalogAdminService service, CancellationToken ct)
    {
        var product = await service.UpdateProductAsync(
            id,
            new ProductData(request.CategoryId, request.Name, request.Slug, request.Description, request.Price),
            request.IsActive,
            ct);
        return TypedResults.Ok(ToResponse(product));
    }

    private static async Task<NoContent> DeactivateProduct(
        Guid id, ICatalogAdminService service, CancellationToken ct)
    {
        await service.DeactivateProductAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static AdminCategoryResponse ToResponse(Category c) => new(c.Id, c.Name, c.Slug, c.ParentId);

    private static AdminProductResponse ToResponse(Product p) =>
        new(p.Id, p.CategoryId, p.Name, p.Slug, p.Description, p.Price, CatalogDefaults.Currency, p.IsActive);
}