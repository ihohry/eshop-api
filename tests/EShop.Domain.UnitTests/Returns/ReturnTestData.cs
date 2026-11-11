using EShop.Domain.Orders;
using EShop.Domain.Returns;
using EShop.Domain.UnitTests.Orders;

namespace EShop.Domain.UnitTests.Returns;

internal static class ReturnTestData
{
    public static readonly Guid ProductA = Guid.Parse("0196a5c0-0000-7000-8000-0000000000a1");
    public static readonly Guid ProductB = Guid.Parse("0196a5c0-0000-7000-8000-0000000000b2");
    public static readonly TimeSpan Window = TimeSpan.FromDays(14);
    public static readonly DateTimeOffset DeliveredAt = OrderTestData.Later;
    public static readonly DateTimeOffset Deadline = DeliveredAt + Window;
    public static readonly DateTimeOffset Now = DeliveredAt.AddDays(1);
    public static readonly DateTimeOffset LaterDecision = Now.AddHours(1);

    public static Order DeliveredOrder()
    {
        var order = OrderTestData.Create(lines: new[]
        {
            new OrderLine(ProductA, "Product A", 10m, 3),
            new OrderLine(ProductB, "Product B", 5.50m, 2),
        });
        order.MarkPaid(OrderTestData.Later);
        order.MarkShipped(OrderTestData.Later);
        order.MarkDelivered(OrderTestData.Later);
        return order;
    }

    public static ReturnRequest Create(
        Order? order = null,
        string reason = "Broken",
        IReadOnlyCollection<ReturnLine>? lines = null,
        IReadOnlyCollection<ReturnedQuantity>? alreadyReturned = null,
        DateTimeOffset? now = null) =>
        ReturnRequest.Create(
            order ?? DeliveredOrder(),
            reason,
            lines ?? new[] { new ReturnLine(ProductA, 1) },
            alreadyReturned ?? Array.Empty<ReturnedQuantity>(),
            now ?? Now,
            Window);

    public static ReturnRequest InStatus(ReturnStatus status)
    {
        var request = Create();
        switch (status)
        {
            case ReturnStatus.Requested:
                break;
            case ReturnStatus.Approved:
                request.Approve(Now);
                break;
            case ReturnStatus.Received:
                request.Approve(Now);
                request.MarkReceived(Now);
                break;
            case ReturnStatus.Refunded:
                request.Approve(Now);
                request.MarkReceived(Now);
                request.MarkRefunded(Now);
                break;
            case ReturnStatus.Rejected:
                request.Reject(Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return request;
    }
}