namespace EShop.Domain.Orders;

// FullName and Phone are the RECIPIENT (can be another person).
// For PickupPoint, the address fields are the address of the point.
public sealed record ShippingAddress(
    string FullName, string Phone, string Line1, string? Line2,
    string PostalCode, string City, string CountryCode);