namespace EShop.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem() { ProductName = null!; }

    internal OrderItem(Guid orderId, OrderLine line)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        ProductId = line.ProductId;
        ProductName = line.ProductName;
        UnitPrice = line.UnitPrice;
        Quantity = line.Quantity;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;
}