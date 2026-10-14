using EShop.Domain.Catalog;
using EShop.Domain.Exceptions;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EShop.Infrastructure.Catalog;

public sealed class CatalogAdminService(AppDbContext db, TimeProvider clock) : ICatalogAdminService
{
    public async Task<Category> CreateCategoryAsync(string name, string slug, Guid? parentId, CancellationToken ct)
    {
        Category? parent = null;
        if (parentId is { } id)
        {
            parent = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                ?? throw new DomainException("Parent category does not exist.");

            if (parent.ParentId is not null)
            {
                throw new DomainException("Categories have only two levels.");
            }
        }

        var category = Category.Create(name, slug, parent, clock.GetUtcNow());
        db.Categories.Add(category);
        await SaveAsync("A category with this slug already exists.", ct);
        return category;
    }

    public async Task<Category> UpdateCategoryAsync(Guid id, string name, string slug, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found.");

        category.Update(name, slug);
        await SaveAsync("A category with this slug already exists.", ct);
        return category;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found.");

        var inUse = await db.Categories.AnyAsync(c => c.ParentId == id, ct)
            || await db.Products.AnyAsync(p => p.CategoryId == id, ct);
        if (inUse)
        {
            throw new ConflictException("The category has subcategories or products and cannot be deleted.");
        }

        db.Categories.Remove(category);
        await SaveAsync("The category cannot be deleted.", ct);
    }

    public async Task<Product> CreateProductAsync(ProductData data, CancellationToken ct)
    {
        await EnsureCategoryExistsAsync(data.CategoryId, ct);

        var product = Product.Create(
            data.CategoryId, data.Name, data.Slug, data.Description, data.Price, clock.GetUtcNow());
        db.Products.Add(product);
        await SaveAsync("A product with this slug already exists.", ct);
        return product;
    }

    public async Task<Product> UpdateProductAsync(
        Guid id, ProductData data, bool isActive, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product not found.");

        await EnsureCategoryExistsAsync(data.CategoryId, ct);

        var now = clock.GetUtcNow();
        product.Update(data.CategoryId, data.Name, data.Slug, data.Description, data.Price, now);
        if (isActive)
        {
            product.Activate(now);
        }
        else
        {
            product.Deactivate(now);
        }

        await SaveAsync("A product with this slug already exists.", ct);
        return product;
    }

    public async Task DeactivateProductAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product not found.");

        product.Deactivate(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            throw new DomainException("Category does not exist.");
        }
    }

    // The unique index is the real guard against duplicate slugs (also under a race).
    private async Task SaveAsync(string duplicateMessage, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (HasSqlState(ex, PostgresErrorCodes.UniqueViolation))
        {
            throw new ConflictException(duplicateMessage);
        }
        catch (DbUpdateException ex) when (HasSqlState(ex, PostgresErrorCodes.ForeignKeyViolation))
        {
            throw new ConflictException("The request conflicts with a related record. Please try again.");
        }
    }

    private static bool HasSqlState(DbUpdateException ex, string sqlState) =>
        ex.InnerException is PostgresException pg && pg.SqlState == sqlState;
}