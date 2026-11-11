using EShop.Domain.Catalog;

namespace EShop.Domain.UnitTests.Catalog;

public class CategoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Category Root(string name = "Fruit", string slug = "fruit") =>
        Category.Create(name, slug, null, Now);

    [Fact]
    public void Top_level_category_has_no_parent()
    {
        var category = Root();

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Fruit", category.Name);
        Assert.Equal("fruit", category.Slug);
        Assert.Null(category.ParentId);
        Assert.Equal(Now, category.CreatedAt);
    }

    [Fact]
    public void Subcategory_points_to_its_parent()
    {
        var parent = Root();

        var child = Category.Create("Apples", "apples", parent, Now);

        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void Third_level_is_rejected()
    {
        var parent = Root();
        var child = Category.Create("Apples", "apples", parent, Now);

        Assert.Throws<ArgumentException>(() => Category.Create("Red apples", "red-apples", child, Now));
    }

    [Fact]
    public void Name_is_trimmed()
    {
        var category = Root("  Fruit  ");

        Assert.Equal("Fruit", category.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_name_is_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => Root(name));
    }

    [Fact]
    public void Name_at_the_maximum_length_is_accepted()
    {
        var name = new string('a', CatalogDefaults.NameMaxLength);

        Assert.Equal(name, Root(name).Name);
    }

    [Fact]
    public void Name_above_the_maximum_length_is_rejected()
    {
        var name = new string('a', CatalogDefaults.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => Root(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Fruit")]
    [InlineData("red apples")]
    [InlineData("red--apples")]
    [InlineData("-apples")]
    [InlineData("apples-")]
    [InlineData("red_apples")]
    [InlineData("jablíčka")]
    [InlineData(" apples")]
    public void Invalid_slug_is_rejected(string slug)
    {
        Assert.Throws<ArgumentException>(() => Root(slug: slug));
    }

    [Theory]
    [InlineData("fruit")]
    [InlineData("red-apples")]
    [InlineData("apples-2")]
    [InlineData("42")]
    public void Valid_slug_is_accepted(string slug)
    {
        Assert.Equal(slug, Root(slug: slug).Slug);
    }

    [Fact]
    public void Slug_at_the_maximum_length_is_accepted()
    {
        var slug = new string('a', CatalogDefaults.SlugMaxLength);

        Assert.Equal(slug, Root(slug: slug).Slug);
    }

    [Fact]
    public void Slug_above_the_maximum_length_is_rejected()
    {
        var slug = new string('a', CatalogDefaults.SlugMaxLength + 1);

        Assert.Throws<ArgumentException>(() => Root(slug: slug));
    }

    [Fact]
    public void Update_changes_name_and_slug_but_not_the_parent()
    {
        var parent = Root();
        var child = Category.Create("Apples", "apples", parent, Now);

        child.Update("  Pears  ", "pears");

        Assert.Equal("Pears", child.Name);
        Assert.Equal("pears", child.Slug);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void Update_with_blank_name_is_rejected()
    {
        var category = Root();

        Assert.Throws<ArgumentException>(() => category.Update(" ", "pears"));
        Assert.Equal("Fruit", category.Name);
    }

    [Fact]
    public void Update_with_invalid_slug_is_rejected_and_changes_nothing()
    {
        var category = Root();

        Assert.Throws<ArgumentException>(() => category.Update("Pears", "Not Valid"));
        Assert.Equal("Fruit", category.Name);
        Assert.Equal("fruit", category.Slug);
    }
}