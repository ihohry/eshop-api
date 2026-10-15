using Microsoft.AspNetCore.Mvc;
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
        // Antiforgery is off on purpose: the API uses bearer tokens, not cookies.
        // 21 MB is the highest file size the options allow (20 MB) plus form overhead.
        group.MapPost("/products/{id:guid}/images", UploadProductImage)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(21 * 1024 * 1024));
        group.MapDelete("/products/{id:guid}/images/{imageId:guid}", DeleteProductImage);

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

    private static async Task<Created<ProductImageResponse>> UploadProductImage(
        Guid id,
        IFormFile file,
        [FromForm] string? altText,
        ICatalogAdminService service,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var image = await service.AddProductImageAsync(id, stream, file.Length, altText, ct);
        return TypedResults.Created($"/images/{image.FileName}", ProductImageResponse.From(image));
    }

    private static async Task<NoContent> DeleteProductImage(
        Guid id, Guid imageId, ICatalogAdminService service, CancellationToken ct)
    {
        await service.DeleteProductImageAsync(id, imageId, ct);
        return TypedResults.NoContent();
    }
}