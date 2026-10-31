using EShop.Domain.Payments;

namespace EShop.Api.Payments;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/stripe", async (
                HttpRequest request, IPaymentWebhookService service, CancellationToken ct) =>
            {
                // Raw body: the signature is calculated over the exact bytes.
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(ct);
                var result = await service.HandleAsync(
                    payload, request.Headers["Stripe-Signature"].ToString(), ct);
                return result == PaymentWebhookResult.InvalidSignature
                    ? Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request")
                    : Results.Ok();
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }
}