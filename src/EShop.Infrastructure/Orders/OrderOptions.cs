using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Orders;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    [Range(15, 7 * 24 * 60)]
    public int PaymentWindowMinutes { get; init; } = 24 * 60;

    [Range(10, 3600)]
    public int ExpiryCheckIntervalSeconds { get; init; } = 60;

    public bool PayOnReceiptEnabled { get; init; }

    [Range(1.0, 10000.0)]
    public decimal PayOnReceiptMaxTotal { get; init; } = 300;

    public bool PayOnReceiptAutoConfirm { get; init; }

    [Range(1, 90)]
    public int ReturnWindowDays { get; init; } = 14;

    public TimeSpan ReturnWindow => TimeSpan.FromDays(ReturnWindowDays);
}