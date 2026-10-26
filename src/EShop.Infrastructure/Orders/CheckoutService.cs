using EShop.Domain.Carts;
using EShop.Domain.Catalog;
using EShop.Domain.Orders;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Orders;

public sealed class CheckoutService(AppDbContext db, TimeProvider time, IOptions<OrderOptions> options)
    : ICheckoutService
{
    public async Task<CheckoutResult> CheckoutAsync(
        Guid userId, ShippingAddress address, DeliveryMethod method,
        PickupPoint? pickupPoint, PaymentMethod paymentMethod, CancellationToken ct)
    {
        var payOnReceipt = paymentMethod == PaymentMethod.PayOnReceipt;
        if (payOnReceipt && !options.Value.PayOnReceiptEnabled)
            return new CheckoutResult(CheckoutStatus.PayOnReceiptNotAvailable);

        var cart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null || cart.Items.Count == 0)
            return new CheckoutResult(CheckoutStatus.EmptyCart);

        // The same order in every request, so two checkouts cannot block each other in a cycle.
        var items = cart.Items.OrderBy(i => i.ProductId).ToList();

        var products = await LoadProductsAsync(items, ct);
        var problems = FindProblems(items, products);
        if (problems.Count > 0)
            return new CheckoutResult(CheckoutStatus.ItemsUnavailable, Problems: problems);

        var total = items.Sum(i => products[i.ProductId].Price * i.Quantity);
        if (payOnReceipt && total > options.Value.PayOnReceiptMaxTotal)
            return new CheckoutResult(CheckoutStatus.PayOnReceiptLimitExceeded);

        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var item in items)
        {
            var productId = item.ProductId;
            var quantity = item.Quantity;

            // One atomic UPDATE per line: it succeeds only if the stock still covers the quantity.
            var changed = await db.Products
                .Where(p => p.Id == productId && p.IsActive && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity)
                    .SetProperty(p => p.UpdatedAt, now), ct);

            if (changed == 0)
            {
                await tx.RollbackAsync(ct);
                return await LostRaceAsync(items, item, ct);
            }
        }

        var lines = items
            .Select(i => new OrderLine(i.ProductId, products[i.ProductId].Name,
                products[i.ProductId].Price, i.Quantity))
            .ToList();

        var order = Order.Create(userId, address, new OrderDelivery(method, pickupPoint),
            paymentMethod, lines, now,
            TimeSpan.FromMinutes(options.Value.PaymentWindowMinutes));
        if (payOnReceipt && options.Value.PayOnReceiptAutoConfirm)
            order.Confirm(now);
        db.Orders.Add(order);
        cart.Clear(now);

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A parallel request already changed or deleted the cart items; the transaction rolls back.
            return new CheckoutResult(CheckoutStatus.CartChanged);
        }

        return new CheckoutResult(CheckoutStatus.Created, order);
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(
        IReadOnlyList<CartItem> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.ProductId).ToList();
        return await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    private static List<CheckoutProblem> FindProblems(
        IReadOnlyList<CartItem> items, Dictionary<Guid, Product> products)
    {
        var problems = new List<CheckoutProblem>();
        foreach (var item in items)
        {
            var product = products[item.ProductId];
            var reason = GetProblem(product, item.Quantity);
            if (reason is not null)
                problems.Add(new CheckoutProblem(product.Id, product.Name, item.Quantity, reason.Value));
        }
        return problems;
    }

    private static CheckoutProblemReason? GetProblem(Product product, int quantity)
    {
        if (!product.IsActive) return CheckoutProblemReason.ProductUnavailable;
        if (product.StockQuantity < quantity) return CheckoutProblemReason.InsufficientStock;
        return null;
    }

    // Another checkout took the stock between our check and our UPDATE: report the current state.
    private async Task<CheckoutResult> LostRaceAsync(
        IReadOnlyList<CartItem> items, CartItem failed, CancellationToken ct)
    {
        var fresh = await LoadProductsAsync(items, ct);
        var problems = FindProblems(items, fresh);
        if (problems.Count == 0)
        {
            problems.Add(new CheckoutProblem(failed.ProductId, fresh[failed.ProductId].Name,
                failed.Quantity, CheckoutProblemReason.InsufficientStock));
        }
        return new CheckoutResult(CheckoutStatus.ItemsUnavailable, Problems: problems);
    }
}