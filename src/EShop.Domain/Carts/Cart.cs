namespace EShop.Domain.Carts;

public static class CartDefaults
{
    public const int MaxQuantityPerItem = 99;
    public const int MaxDistinctItems = 99;
}

public sealed class CartItem
{
    private CartItem() { }

    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    internal static CartItem Create(Guid cartId, Guid productId, int quantity, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CartId = cartId,
            ProductId = productId,
            Quantity = quantity,
            AddedAt = now
        };

    internal void SetQuantity(int quantity) => Quantity = quantity;
}

public sealed class Cart
{
    private readonly List<CartItem> _items = [];

    private Cart() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<CartItem> Items => _items;

    public static Cart Create(Guid userId, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(), UserId = userId, UpdatedAt = now };

    public void AddItem(Guid productId, int quantity, DateTimeOffset now)
    {
        ValidateQuantity(quantity);
        var item = _items.Find(i => i.ProductId == productId);
        if (item is null)
        {
            if (_items.Count >= CartDefaults.MaxDistinctItems)
                throw new InvalidOperationException("The cart has too many different products.");
            _items.Add(CartItem.Create(Id, productId, quantity, now));
        }
        else
        {
            var total = item.Quantity + quantity;
            ValidateQuantity(total);
            item.SetQuantity(total);
        }
        UpdatedAt = now;
    }

    public bool SetQuantity(Guid productId, int quantity, DateTimeOffset now)
    {
        ValidateQuantity(quantity);
        var item = _items.Find(i => i.ProductId == productId);
        if (item is null) return false;
        item.SetQuantity(quantity);
        UpdatedAt = now;
        return true;
    }

    public bool RemoveItem(Guid productId, DateTimeOffset now)
    {
        var removed = _items.RemoveAll(i => i.ProductId == productId) > 0;
        if (removed) UpdatedAt = now;
        return removed;
    }

    public void Clear(DateTimeOffset now)
    {
        _items.Clear();
        UpdatedAt = now;
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > CartDefaults.MaxQuantityPerItem)
            throw new ArgumentOutOfRangeException(nameof(quantity),
                $"Quantity must be between 1 and {CartDefaults.MaxQuantityPerItem}.");
    }
}