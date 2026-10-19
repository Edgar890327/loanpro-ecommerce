using Microsoft.Extensions.Options;
using Store.Api.Auth;
using Store.UnitTests.Identity;

namespace Store.UnitTests;

public sealed class EmailAttemptLimiterTests
{
    [Fact]
    public void TryAcquire_BlocksAfterTheLimitPerEmailAndResetsAfterTheWindow()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        var limiter = new EmailAttemptLimiter(Options.Create(new AuthOptions { RateLimitPerMinute = 2 }), clock);

        Assert.True(limiter.TryAcquire("ana@example.com"));
        Assert.True(limiter.TryAcquire("ANA@example.com"));
        Assert.False(limiter.TryAcquire("ana@example.com"));
        Assert.True(limiter.TryAcquire("other@example.com"));

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.True(limiter.TryAcquire("ana@example.com"));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now += span;
    }
}
