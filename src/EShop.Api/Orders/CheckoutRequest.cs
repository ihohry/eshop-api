using System.ComponentModel.DataAnnotations;
using EShop.Domain.Orders;

namespace EShop.Api.Orders;

public sealed record CheckoutRequest(
    [property: Required, AllowedValues("Courier", "PickupPoint")] string? DeliveryMethod,
    [property: Required] ShippingAddressRequest? ShippingAddress,
    PickupPointRequest? PickupPoint,
    [property: RegularExpression("^(Prepaid|PayOnReceipt)$")] string? PaymentMethod = null);

public sealed record ShippingAddressRequest(
    [property: Required, StringLength(OrderDefaults.MaxTextLength)] string FullName,
    [property: Required, RegularExpression(OrderDefaults.PhonePatternText)] string Phone,
    [property: Required, StringLength(OrderDefaults.MaxTextLength)] string Line1,
    [property: StringLength(OrderDefaults.MaxTextLength)] string? Line2,
    [property: Required, StringLength(20)] string PostalCode,
    [property: Required, StringLength(OrderDefaults.MaxTextLength)] string City,
    [property: Required, RegularExpression("^[A-Z]{2}$")] string CountryCode);

public sealed record PickupPointRequest(
    [property: Required, StringLength(OrderDefaults.MaxPickupPointIdLength)] string Id,
    [property: Required, StringLength(OrderDefaults.MaxTextLength)] string Name);