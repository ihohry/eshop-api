using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Orders;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    [Range(15, 7 * 24 * 60)]
    public int PaymentWindowMinutes { get; init; } = 24 * 60;

    [Range(10, 3600)]
    public int ExpiryCheckIntervalSeconds { get; init; } = 60;
}