namespace EShop.Domain.Returns;

public sealed class ReturnItem
{
    private ReturnItem() { }

    internal ReturnItem(Guid returnRequestId, ReturnLine line)
    {
        ReturnRequestId = returnRequestId;
        ProductId = line.ProductId;
        Quantity = line.Quantity;
    }

    public Guid ReturnRequestId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
}