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
}