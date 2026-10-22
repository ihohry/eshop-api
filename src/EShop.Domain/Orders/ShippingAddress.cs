namespace EShop.Domain.Orders;

// FullName and Phone are the RECIPIENT (can be another person).
public sealed record ShippingAddress(
    string FullName, string Phone, string Line1, string? Line2,
    string PostalCode, string City, string CountryCode);