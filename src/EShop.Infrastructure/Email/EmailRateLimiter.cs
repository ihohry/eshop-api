using System.Security.Cryptography;
using System.Text;
using EShop.Domain.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Email;

public sealed partial class EmailRateLimiter(
    IOptions<EmailRateLimitOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailRateLimiter> logger) : IEmailRateLimiter
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<string, WindowCounter> entries = new(StringComparer.Ordinal);
    private DateTimeOffset lastSweep = timeProvider.GetUtcNow();

    public bool TryAcquire(string email)
    {
        var settings = options.Value;
        var window = TimeSpan.FromSeconds(settings.WindowSeconds);
        var key = Hash(email);
        var now = timeProvider.GetUtcNow();

        lock (gate)
        {
            if (now - lastSweep >= SweepInterval)
            {
                RemoveExpired(now, window);
                lastSweep = now;
            }

            if (entries.TryGetValue(key, out var counter))
            {
                if (now - counter.Start >= window)
                {
                    counter.Start = now;
                    counter.Count = 0;
                }

                if (counter.Count >= settings.PermitLimit)
                {
                    LogLimitReached(logger);
                    return false;
                }

                counter.Count++;
                return true;
            }

            if (entries.Count >= settings.MaxEntries)
            {
                RemoveExpired(now, window);

                if (entries.Count >= settings.MaxEntries)
                {
                    LogTableFull(logger);
                    return false;
                }
            }

            entries[key] = new WindowCounter { Start = now, Count = 1 };
            return true;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email request limit reached, nothing sent")]
    private static partial void LogLimitReached(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email rate limiter is full, a new address was refused")]
    private static partial void LogTableFull(ILogger logger);

    private static string Hash(string email)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private void RemoveExpired(DateTimeOffset now, TimeSpan window)
    {
        var expired = entries
            .Where(pair => now - pair.Value.Start >= window)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expired)
        {
            entries.Remove(key);
        }
    }

    private sealed class WindowCounter
    {
        public DateTimeOffset Start { get; set; }

        public int Count { get; set; }
    }
}