namespace EShop.Domain.Catalog;

public sealed class Category
{
    private Category() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Category Create(string name, string slug, Category? parent, DateTimeOffset now)
    {
        // Two levels only: a subcategory cannot have its own subcategories.
        if (parent?.ParentId is not null)
        {
            throw new ArgumentException("Categories have only two levels.", nameof(parent));
        }

        return new Category
        {
            Id = Guid.CreateVersion7(),
            Name = CatalogValidation.Name(name, nameof(name)),
            Slug = CatalogValidation.Slug(slug, nameof(slug)),
            ParentId = parent?.Id,
            CreatedAt = now,
        };
    }

    public void Update(string name, string slug)
    {
        Name = CatalogValidation.Name(name, nameof(name));
        Slug = CatalogValidation.Slug(slug, nameof(slug));
    }
}