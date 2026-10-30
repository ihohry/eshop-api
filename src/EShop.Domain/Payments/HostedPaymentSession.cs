namespace EShop.Domain.Payments;

/// <summary>A payment page created at the provider (result of the gateway, T5.2).</summary>
public sealed record HostedPaymentSession(string Id, string Url, DateTimeOffset ExpiresAt);