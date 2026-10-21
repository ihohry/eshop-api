namespace EShop.Domain.Orders;

// For PickupPoint, the address fields are the address of the point.
public sealed record PickupPoint(string Id, string Name);