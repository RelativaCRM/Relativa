using System.Collections.Concurrent;
using Relativa.Authentication.Application.Interfaces;

namespace Relativa.Authentication.Infrastructure.Services;

public sealed class MemoryEmailRateLimiter : IEmailRateLimiter
{
    private sealed class Window
    {
        public int Count;
        public DateTime ResetAt;
    }

    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public bool TryConsume(string key, int permitLimit, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var entry = _windows.AddOrUpdate(
            key,
            _ => new Window { Count = 1, ResetAt = now + window },
            (_, existing) =>
            {
                lock (existing)
                {
                    if (now >= existing.ResetAt)
                    {
                        existing.Count = 1;
                        existing.ResetAt = now + window;
                    }
                    else
                    {
                        existing.Count++;
                    }
                }

                return existing;
            });

        return entry.Count <= permitLimit;
    }
}
