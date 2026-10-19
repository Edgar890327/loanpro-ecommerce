using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Store.Application;

namespace Store.Api.Auth;

public sealed class EmailAttemptLimiter(IOptions<AuthOptions> options, TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.Ordinal);

    public bool TryAcquire(string? email)
    {
        var now = timeProvider.GetUtcNow();
        Purge(now);
        var key = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(EmailNormalizer.Normalize(email ?? string.Empty))));
        var limit = options.Value.RateLimitPerMinute;
        var counter = _counters.GetOrAdd(key, _ => new Counter(now));
        lock (counter)
        {
            if (now - counter.WindowStart >= Window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            if (counter.Count >= limit)
            {
                return false;
            }

            counter.Count++;
            return true;
        }
    }

    private void Purge(DateTimeOffset now)
    {
        if (_counters.Count < 1024)
        {
            return;
        }

        foreach (var (key, counter) in _counters)
        {
            if (now - counter.WindowStart >= Window)
            {
                _counters.TryRemove(key, out _);
            }
        }
    }

    private sealed class Counter(DateTimeOffset windowStart)
    {
        public DateTimeOffset WindowStart { get; set; } = windowStart;

        public int Count { get; set; }
    }
}
