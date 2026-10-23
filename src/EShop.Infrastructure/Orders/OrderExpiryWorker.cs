using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Orders;

public sealed partial class OrderExpiryWorker(
    IServiceScopeFactory scopes,
    IOptions<OrderOptions> options,
    ILogger<OrderExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ExpiryCheckIntervalSeconds));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<OrderExpiryService>();
                await service.ExpireDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRunFailed(ex); // the next tick tries again
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Order expiry run failed")]
    private partial void LogRunFailed(Exception exception);
}