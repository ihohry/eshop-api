using EShop.Domain.Carts;
using Xunit;

namespace EShop.Domain.UnitTests.Carts;

public sealed class CartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private static Cart NewCart() => Cart.Create(Guid.CreateVersion7(), Now);

    [Fact]
    public void Create_makes_an_empty_cart_for_the_user()
    {
        var userId = Guid.CreateVersion7();

        var cart = Cart.Create(userId, Now);

        Assert.NotEqual(Guid.Empty, cart.Id);
        Assert.Equal(userId, cart.UserId);
        Assert.Empty(cart.Items);
        Assert.Equal(Now, cart.UpdatedAt);
    }

    [Fact]
    public void AddItem_adds_a_new_line()
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();
        var later = Now.AddMinutes(1);

        cart.AddItem(productId, 2, later);

        var item = Assert.Single(cart.Items);
        Assert.Equal(cart.Id, item.CartId);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(later, item.AddedAt);
        Assert.Equal(later, cart.UpdatedAt);
    }

    [Fact]
    public void AddItem_merges_quantities_for_the_same_product()
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();

        cart.AddItem(productId, 2, Now);
        cart.AddItem(productId, 3, Now.AddMinutes(1));

        var item = Assert.Single(cart.Items);
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void AddItem_accepts_a_total_of_exactly_the_limit()
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();

        cart.AddItem(productId, CartDefaults.MaxQuantityPerItem - 1, Now);
        cart.AddItem(productId, 1, Now);

        Assert.Equal(CartDefaults.MaxQuantityPerItem, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void AddItem_rejects_a_merged_total_over_the_limit_and_keeps_the_line()
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();
        cart.AddItem(productId, CartDefaults.MaxQuantityPerItem, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddItem(productId, 1, Now.AddMinutes(1)));

        Assert.Equal(CartDefaults.MaxQuantityPerItem, Assert.Single(cart.Items).Quantity);
        Assert.Equal(Now, cart.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CartDefaults.MaxQuantityPerItem + 1)]
    public void AddItem_rejects_a_quantity_outside_the_range(int quantity)
    {
        var cart = NewCart();

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddItem(Guid.CreateVersion7(), quantity, Now));

        Assert.Empty(cart.Items);
    }

    [Fact]
    public void AddItem_rejects_a_new_product_when_the_cart_is_full()
    {
        var cart = NewCart();
        for (var i = 0; i < CartDefaults.MaxDistinctItems; i++)
        {
            cart.AddItem(Guid.CreateVersion7(), 1, Now);
        }

        Assert.Throws<InvalidOperationException>(() => cart.AddItem(Guid.CreateVersion7(), 1, Now));

        Assert.Equal(CartDefaults.MaxDistinctItems, cart.Items.Count);
    }

    [Fact]
    public void AddItem_still_allows_an_existing_product_when_the_cart_is_full()
    {
        var cart = NewCart();
        var first = Guid.CreateVersion7();
        cart.AddItem(first, 1, Now);
        for (var i = 1; i < CartDefaults.MaxDistinctItems; i++)
        {
            cart.AddItem(Guid.CreateVersion7(), 1, Now);
        }

        cart.AddItem(first, 2, Now);

        Assert.Equal(3, cart.Items.Single(i => i.ProductId == first).Quantity);
    }

    [Fact]
    public void SetQuantity_replaces_the_quantity_of_an_existing_line()
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();
        cart.AddItem(productId, 2, Now);
        var later = Now.AddMinutes(1);

        var found = cart.SetQuantity(productId, 7, later);

        Assert.True(found);
        Assert.Equal(7, Assert.Single(cart.Items).Quantity);
        Assert.Equal(later, cart.UpdatedAt);
    }

    [Fact]
    public void SetQuantity_returns_false_for_a_product_not_in_the_cart()
    {
        var cart = NewCart();

        var found = cart.SetQuantity(Guid.CreateVersion7(), 3, Now.AddMinutes(1));

        Assert.False(found);
        Assert.Equal(Now, cart.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CartDefaults.MaxQuantityPerItem + 1)]
    public void SetQuantity_rejects_a_quantity_outside_the_range(int quantity)
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();
        cart.AddItem(productId, 2, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.SetQuantity(productId, quantity, Now));

        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void RemoveItem_removes_the_line_and_touches_updated_at()
    {
        var cart = NewCart();
        var productId = Guid.CreateVersion7();
        cart.AddItem(productId, 2, Now);
        var later = Now.AddMinutes(1);

        var removed = cart.RemoveItem(productId, later);

        Assert.True(removed);
        Assert.Empty(cart.Items);
        Assert.Equal(later, cart.UpdatedAt);
    }

    [Fact]
    public void RemoveItem_returns_false_for_a_product_not_in_the_cart()
    {
        var cart = NewCart();

        var removed = cart.RemoveItem(Guid.CreateVersion7(), Now.AddMinutes(1));

        Assert.False(removed);
        Assert.Equal(Now, cart.UpdatedAt);
    }

    [Fact]
    public void Clear_removes_all_lines()
    {
        var cart = NewCart();
        cart.AddItem(Guid.CreateVersion7(), 1, Now);
        cart.AddItem(Guid.CreateVersion7(), 2, Now);
        var later = Now.AddMinutes(1);

        cart.Clear(later);

        Assert.Empty(cart.Items);
        Assert.Equal(later, cart.UpdatedAt);
    }
}