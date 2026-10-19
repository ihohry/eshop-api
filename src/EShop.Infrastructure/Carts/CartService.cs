using EShop.Domain.Carts;
using EShop.Domain.Catalog;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.Infrastructure.Carts;

public sealed class CartService(AppDbContext db, TimeProvider time) : ICartService
{
    public async Task<CartView> GetAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Carts
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .SelectMany(c => c.Items)
            .Join(db.Products, i => i.ProductId, p => p.Id, (i, p) => new
            {
                i.ProductId, i.AddedAt, i.Quantity,
                p.Name, p.Slug, p.Price, p.IsActive, p.StockQuantity
            })
            .ToListAsync(ct);

        var items = rows
            .OrderBy(r => r.AddedAt).ThenBy(r => r.ProductId)
            .Select(r => new CartItemView(
                r.ProductId, r.Name, r.Slug, r.Price, r.Quantity, r.Price * r.Quantity,
                r.IsActive && r.StockQuantity >= r.Quantity))
            .ToList();

        return new CartView(
            items,
            items.Sum(i => i.Quantity),
            items.Where(i => i.IsAvailable).Sum(i => i.LineTotal),
            CatalogDefaults.Currency);
    }

    public async Task<CartOperationResult> AddItemAsync(
        Guid userId, Guid productId, int quantity, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct))
            return CartOperationResult.ProductNotFound;

        var now = time.GetUtcNow();
        var cart = await LoadAsync(userId, ct);
        if (cart is null)
        {
            cart = Cart.Create(userId, now);
            db.Carts.Add(cart);
        }

        var current = cart.Items.FirstOrDefault(i => i.ProductId == productId)?.Quantity ?? 0;
        if (current + quantity > CartDefaults.MaxQuantityPerItem
            || (current == 0 && cart.Items.Count >= CartDefaults.MaxDistinctItems))
            return CartOperationResult.LimitExceeded;

        cart.AddItem(productId, quantity, now);
        await db.SaveChangesAsync(ct);
        return CartOperationResult.Ok;
    }

    public async Task<CartOperationResult> SetQuantityAsync(
        Guid userId, Guid productId, int quantity, CancellationToken ct)
    {
        var cart = await LoadAsync(userId, ct);
        if (cart is null || !cart.SetQuantity(productId, quantity, time.GetUtcNow()))
            return CartOperationResult.ItemNotFound;

        await db.SaveChangesAsync(ct);
        return CartOperationResult.Ok;
    }

    public async Task RemoveItemAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        var cart = await LoadAsync(userId, ct);
        if (cart is not null && cart.RemoveItem(productId, time.GetUtcNow()))
            await db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(Guid userId, CancellationToken ct)
    {
        var cart = await LoadAsync(userId, ct);
        if (cart is null || cart.Items.Count == 0) return;
        cart.Clear(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    private Task<Cart?> LoadAsync(Guid userId, CancellationToken ct) =>
        db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
}