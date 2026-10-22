namespace EShop.Domain.Orders;

public sealed record OrderPage(IReadOnlyList<OrderSummary> Items, int Page, int PageSize, int TotalCount);