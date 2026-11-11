using EShop.Domain.Catalog;
using Xunit;

namespace EShop.Domain.UnitTests.Catalog;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid CategoryId = Guid.CreateVersion7();

    private static Product NewProduct() =>
        Product.Create(CategoryId, "Laptop", "laptop", "Fast", 999.99m, Now);

    [Fact]
    public void Create_sets_defaults()
    {
        var product = NewProduct();

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(CategoryId, product.CategoryId);
        Assert.True(product.IsActive);
        Assert.Equal(0, product.StockQuantity);
        Assert.Equal(Product.DefaultWarrantyMonths, product.WarrantyMonths);
        Assert.Equal(Now, product.CreatedAt);
        Assert.Equal(Now, product.UpdatedAt);
    }

    [Fact]
    public void Create_rejects_empty_category()
    {
        Assert.Throws<ArgumentException>(() =>
            Product.Create(Guid.Empty, "Laptop", "laptop", null, 10m, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Product.Create(CategoryId, name, "laptop", null, 10m, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not A Slug")]
    public void Create_rejects_invalid_slug(string slug)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Product.Create(CategoryId, "Laptop", slug, null, 10m, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    public void Create_rejects_invalid_price(double price)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Product.Create(CategoryId, "Laptop", "laptop", null, (decimal)price, Now));
    }

    [Fact]
    public void Create_accepts_price_with_two_decimals()
    {
        var product = Product.Create(CategoryId, "Laptop", "laptop", null, 10.99m, Now);

        Assert.Equal(10.99m, product.Price);
    }

    [Fact]
    public void Create_accepts_description_at_the_limit()
    {
        var description = new string('a', CatalogDefaults.DescriptionMaxLength);

        var product = Product.Create(CategoryId, "Laptop", "laptop", description, 10m, Now);

        Assert.Equal(description, product.Description);
    }

    [Fact]
    public void Create_rejects_description_over_the_limit()
    {
        var description = new string('a', CatalogDefaults.DescriptionMaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            Product.Create(CategoryId, "Laptop", "laptop", description, 10m, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Product.MaxWarrantyMonths)]
    public void Create_accepts_warranty_at_the_boundaries(int months)
    {
        var product = Product.Create(CategoryId, "Laptop", "laptop", null, 10m, Now, months);

        Assert.Equal(months, product.WarrantyMonths);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Product.MaxWarrantyMonths + 1)]
    public void Create_rejects_warranty_outside_the_range(int months)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.Create(CategoryId, "Laptop", "laptop", null, 10m, Now, months));
    }

    [Fact]
    public void Update_changes_fields_and_keeps_created_at()
    {
        var product = NewProduct();
        var later = Now.AddHours(1);
        var newCategory = Guid.CreateVersion7();

        product.Update(newCategory, "Phone", "phone", null, 499m, 12, later);

        Assert.Equal(newCategory, product.CategoryId);
        Assert.Equal("Phone", product.Name);
        Assert.Equal("phone", product.Slug);
        Assert.Null(product.Description);
        Assert.Equal(499m, product.Price);
        Assert.Equal(12, product.WarrantyMonths);
        Assert.Equal(Now, product.CreatedAt);
        Assert.Equal(later, product.UpdatedAt);
    }

    [Fact]
    public void Update_does_not_change_stock_or_active_flag()
    {
        var product = NewProduct();
        product.SetStock(7, Now);
        product.Deactivate(Now);

        product.Update(CategoryId, "Phone", "phone", null, 499m, 12, Now.AddHours(1));

        Assert.Equal(7, product.StockQuantity);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Update_rejects_invalid_warranty()
    {
        var product = NewProduct();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.Update(CategoryId, "Laptop", "laptop", null, 10m, -1, Now));
    }

    [Fact]
    public void Deactivate_and_Activate_toggle_the_flag_and_touch_updated_at()
    {
        var product = NewProduct();
        var t1 = Now.AddMinutes(1);
        var t2 = Now.AddMinutes(2);

        product.Deactivate(t1);
        Assert.False(product.IsActive);
        Assert.Equal(t1, product.UpdatedAt);

        product.Activate(t2);
        Assert.True(product.IsActive);
        Assert.Equal(t2, product.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(Product.MaxStockQuantity)]
    public void SetStock_accepts_values_in_the_range(int quantity)
    {
        var product = NewProduct();
        var later = Now.AddMinutes(1);

        product.SetStock(quantity, later);

        Assert.Equal(quantity, product.StockQuantity);
        Assert.Equal(later, product.UpdatedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Product.MaxStockQuantity + 1)]
    public void SetStock_rejects_values_outside_the_range_and_keeps_the_old_value(int quantity)
    {
        var product = NewProduct();
        product.SetStock(3, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.SetStock(quantity, Now.AddMinutes(1)));

        Assert.Equal(3, product.StockQuantity);
        Assert.Equal(Now, product.UpdatedAt);
    }
}