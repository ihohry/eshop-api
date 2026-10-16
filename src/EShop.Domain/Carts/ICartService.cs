namespace EShop.Domain.Carts;

public enum CartOperationResult { Ok, ProductNotFound, ItemNotFound, LimitExceeded }

public sealed record CartItemView(
    Guid ProductId, string Name, string Slug, decimal UnitPrice,
    int Quantity, decimal LineTotal, bool IsAvailable);

public sealed record CartView(
    IReadOnlyList<CartItemView> Items, int TotalQuantity, decimal Subtotal, string Currency);

public interface ICartService
{
    Task<CartView> GetAsync(Guid userId, CancellationToken ct);
    Task<CartOperationResult> AddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct);
    Task<CartOperationResult> SetQuantityAsync(Guid userId, Guid productId, int quantity, CancellationToken ct);
    Task RemoveItemAsync(Guid userId, Guid productId, CancellationToken ct);
    Task ClearAsync(Guid userId, CancellationToken ct);
}