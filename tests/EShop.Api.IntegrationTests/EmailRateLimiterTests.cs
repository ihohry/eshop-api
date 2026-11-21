using EShop.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EShop.Api.IntegrationTests;

public sealed class EmailRateLimiterTests
{
    private static (EmailRateLimiter Limiter, FakeTimeProvider Time) Create(
        int permitLimit = 3, int windowSeconds = 3600, int maxEntries = 1000)
    {
        var time = new FakeTimeProvider();
        var options = Options.Create(new EmailRateLimitOptions
        {
            PermitLimit = permitLimit,
            WindowSeconds = windowSeconds,
            MaxEntries = maxEntries,
        });

        return (new EmailRateLimiter(options, time, NullLogger<EmailRateLimiter>.Instance), time);
    }

    [Fact]
    public void Allows_requests_up_to_the_limit_and_refuses_the_next()
    {
        var (limiter, _) = Create();

        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.False(limiter.TryAcquire("a@x.com"));
    }

    [Fact]
    public void Different_spelling_of_one_address_shares_one_counter()
    {
        var (limiter, _) = Create(permitLimit: 2);

        Assert.True(limiter.TryAcquire("A@x.com"));
        Assert.True(limiter.TryAcquire("  a@X.COM "));
        Assert.False(limiter.TryAcquire("a@x.com"));
    }

    [Fact]
    public void Another_address_has_its_own_counter()
    {
        var (limiter, _) = Create(permitLimit: 1);

        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.False(limiter.TryAcquire("a@x.com"));
        Assert.True(limiter.TryAcquire("b@x.com"));
    }

    [Fact]
    public void Counter_starts_again_when_the_window_has_ended()
    {
        var (limiter, time) = Create(permitLimit: 1, windowSeconds: 60);

        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.False(limiter.TryAcquire("a@x.com"));

        time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(limiter.TryAcquire("a@x.com"));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(limiter.TryAcquire("a@x.com"));
    }

    [Fact]
    public void Full_table_refuses_a_new_address_but_still_counts_a_known_one()
    {
        var (limiter, _) = Create(maxEntries: 2);

        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.True(limiter.TryAcquire("b@x.com"));
        Assert.False(limiter.TryAcquire("c@x.com"));
        Assert.True(limiter.TryAcquire("a@x.com"));
    }

    [Fact]
    public void Full_table_accepts_a_new_address_after_old_entries_expired()
    {
        var (limiter, time) = Create(windowSeconds: 60, maxEntries: 2);

        Assert.True(limiter.TryAcquire("a@x.com"));
        Assert.True(limiter.TryAcquire("b@x.com"));
        Assert.False(limiter.TryAcquire("c@x.com"));

        time.Advance(TimeSpan.FromSeconds(60));
        Assert.True(limiter.TryAcquire("c@x.com"));
    }
}