using System.Collections.Concurrent;

namespace Rent.Web.Features.AiChat.Services;

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _hits = new();

    public bool TryAcquire(string key, int limit, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - window;

        var hits = _hits.GetOrAdd(key, _ => new List<DateTimeOffset>());
        lock (hits)
        {
            hits.RemoveAll(t => t < cutoff);
            if (hits.Count >= limit) return false;
            hits.Add(now);
            return true;
        }
    }
}
