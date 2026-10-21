using System.Text.RegularExpressions;

namespace EShop.Domain.Orders;

public static class OrderDefaults
{
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromHours(24);
    public const int MaxTextLength = 200;
    public const int MaxPickupPointIdLength = 50;
    public const int MaxPhoneLength = 16;
    public static readonly Regex PhonePattern = new(@"^\+[1-9]\d{6,14}$", RegexOptions.Compiled);
}