using EShop.Domain.Catalog;
using EShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EShop.Api.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty).WithTags("Catalog");

        group.MapGet("/categories", GetCategories);
        group.MapGet("/products", GetProducts);
        group.MapGet("/products/{slug}", GetProduct);

        return app;
    }

    private static async Task<Ok<List<CategoryResponse>>> GetCategories(
        AppDbContext db, CancellationToken ct)
    {
        // The category tree is small, so we load it once and build it in memory.
        var all = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var childrenByParent = all.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId!.Value);

        var tree = all
            .Where(c => c.ParentId is null)
            .Select(c => new CategoryResponse(
                c.Id,
                c.Name,
                c.Slug,
                childrenByParent[c.Id]
                    .Select(child => new CategoryResponse(child.Id, child.Name, child.Slug, []))
                    .ToList()))
            .ToList();

        return TypedResults.Ok(tree);
    }

    private static async Task<Ok<PagedResponse<ProductResponse>>> GetProducts(
    [AsParameters] ProductListQuery query, AppDbContext db, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var categoryId = await db.Categories
                .Where(c => c.Slug == query.Category)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);

            // Unknown category: empty result, not an error.
            var ids = categoryId is null
                ? []
                : await db.Categories
                    .Where(c => c.Id == categoryId || c.ParentId == categoryId)
                    .Select(c => c.Id)
                    .ToListAsync(ct);

            products = products.Where(p => ids.Contains(p.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
        }

        // Id is the tie-breaker, so paging is stable.
        var ordered = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            ProductSort.Newest => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            _ => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
        };

        var total = await ordered.CountAsync(ct);
        var items = await ToResponses(db, ordered
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<ProductResponse>(items, query.Page, query.PageSize, total));
    }

    private static async Task<Results<Ok<ProductResponse>, NotFound>> GetProduct(
        string slug, AppDbContext db, CancellationToken ct)
    {
        var product = await ToResponses(
                db, db.Products.AsNoTracking().Where(p => p.IsActive && p.Slug == slug))
            .FirstOrDefaultAsync(ct);

        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    // One shared projection, so the list and the detail endpoint cannot drift apart.
    private static IQueryable<ProductResponse> ToResponses(AppDbContext db, IQueryable<Product> products) =>
        products.Select(p => new ProductResponse(
            p.Id,
            p.CategoryId,
            p.Name,
            p.Slug,
            p.Description,
            p.Price,
            CatalogDefaults.Currency,
            db.ProductImages
                .Where(i => i.ProductId == p.Id)
                .OrderBy(i => i.Position)
                .Select(i => new ProductImageResponse(
                    i.Id, ProductImageResponse.UrlPrefix + i.FileName, i.AltText, i.Position))
                .ToList()));

    // Escapes the LIKE wildcards, so user input is searched as plain text.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}