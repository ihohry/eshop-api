using System.Text.RegularExpressions;

namespace EShop.Domain.Orders;

public static class OrderDefaults
{
    public const int MaxTextLength = 200;
    public const int MaxPickupPointIdLength = 50;
    public const int MaxPhoneLength = 16;
    public const string PhonePatternText = @"^\+[1-9]\d{6,14}$";
    public static readonly Regex PhonePattern = new(PhonePatternText, RegexOptions.Compiled);
}