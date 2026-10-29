using EShop.Infrastructure.Images;
using Microsoft.Extensions.Options;
using EShop.Domain.Catalog;
using EShop.Domain.Exceptions;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EShop.Infrastructure.Catalog;

public sealed class CatalogAdminService(
    AppDbContext db,
    TimeProvider clock,
    IImageStorage storage,
    IOptions<ImageStorageOptions> imageOptions) : ICatalogAdminService
{
    private const string ProductNotFound = "Product not found.";
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
            data.CategoryId, data.Name, data.Slug, data.Description, data.Price, clock.GetUtcNow(),
            warrantyMonths: data.WarrantyMonths ?? Product.DefaultWarrantyMonths);
        db.Products.Add(product);
        await SaveAsync("A product with this slug already exists.", ct);
        return product;
    }

    public async Task<Product> UpdateProductAsync(
        Guid id, ProductData data, bool isActive, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(ProductNotFound);

        await EnsureCategoryExistsAsync(data.CategoryId, ct);

        var now = clock.GetUtcNow();
        product.Update(
            data.CategoryId, data.Name, data.Slug, data.Description, data.Price,
            data.WarrantyMonths ?? product.WarrantyMonths, now);
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
            ?? throw new NotFoundException(ProductNotFound);

        product.Deactivate(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    public async Task<Product> SetProductStockAsync(Guid id, int quantity, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(ProductNotFound);

        product.SetStock(quantity, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<ProductImage> AddProductImageAsync(
        Guid productId, Stream content, long length, string? altText, CancellationToken ct)
    {
        var maxSize = imageOptions.Value.MaxFileSizeBytes;
        if (length <= 0 || length > maxSize)
        {
            throw new DomainException($"The image must not be empty and must be at most {maxSize} bytes.");
        }

        if (altText is { Length: > ImageDefaults.AltTextMaxLength })
        {
            throw new DomainException($"Alt text must be at most {ImageDefaults.AltTextMaxLength} characters.");
        }

        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException(ProductNotFound);
        }

        var existing = await db.ProductImages
            .Where(i => i.ProductId == productId)
            .Select(i => i.Position)
            .ToListAsync(ct);
        if (existing.Count >= ImageDefaults.MaxImagesPerProduct)
        {
            throw new ConflictException(
                $"A product can have at most {ImageDefaults.MaxImagesPerProduct} images.");
        }

        // The file signature decides the type; the client's content type and file name are ignored.
        var header = new byte[ImageSignature.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var type = ImageSignature.Detect(header.AsSpan(0, read))
            ?? throw new DomainException("Only JPEG, PNG and WebP images are accepted.");
        content.Seek(0, SeekOrigin.Begin);

        var fileName = await storage.SaveAsync(content, type.Extension, ct);
        try
        {
            var position = existing.Count == 0 ? 0 : existing.Max() + 1;
            var image = ProductImage.Create(
                productId, fileName, type.ContentType, length, position, altText, clock.GetUtcNow());
            db.ProductImages.Add(image);
            await SaveAsync("The image could not be saved.", ct);
            return image;
        }
        catch
        {
            // Do not leave a file without a database row.
            await storage.DeleteAsync(fileName, CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteProductImageAsync(Guid productId, Guid imageId, CancellationToken ct)
    {
        var image = await db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct)
            ?? throw new NotFoundException("Image not found.");

        // Row first, file second: a leftover file is harmless, a row without a file is not.
        db.ProductImages.Remove(image);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(image.FileName, CancellationToken.None);
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