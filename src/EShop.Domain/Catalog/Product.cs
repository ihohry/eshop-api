namespace EShop.Domain.Catalog;

public sealed class Product
{
    public const int MaxStockQuantity = 1_000_000;

    private Product() { }

    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Product Create(
        Guid categoryId, string name, string slug, string? description, decimal price,
        DateTimeOffset now, int stockQuantity = 0)
    {
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            CreatedAt = now,
        };
        product.Apply(categoryId, name, slug, description, price, now);
        product.SetStock(stockQuantity, now);
        return product;
    }

    public void Update(
        Guid categoryId, string name, string slug, string? description, decimal price, DateTimeOffset now)
        => Apply(categoryId, name, slug, description, price, now);

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    public void SetStock(int quantity, DateTimeOffset now)
    {
        if (quantity is < 0 or > MaxStockQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), $"Stock must be between 0 and {MaxStockQuantity}.");
        }

        StockQuantity = quantity;
        UpdatedAt = now;
    }

    private void Apply(
        Guid categoryId, string name, string slug, string? description, decimal price, DateTimeOffset now)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(categoryId));
        }

        if (description is { Length: > CatalogDefaults.DescriptionMaxLength })
        {
            throw new ArgumentException("Description is too long.", nameof(description));
        }

        CategoryId = categoryId;
        Name = CatalogValidation.Name(name, nameof(name));
        Slug = CatalogValidation.Slug(slug, nameof(slug));
        Description = description;
        Price = CatalogValidation.Price(price, nameof(price));
        UpdatedAt = now;
    }
}