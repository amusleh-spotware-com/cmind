using Core.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// A shared, thread-safe request-spacing gate for a primary source's HTTP client, so ingestion never trips a
/// provider's rate limit (FRED allows ~120 req/min). It hands out send slots spaced by <c>60s / rpm</c>, and
/// a <see cref="Penalize"/> call (on a 429) pushes the next slot out by the server's <c>Retry-After</c> so the
/// whole source cools down. Deterministic and clock-injected — no real time in tests.
/// </summary>
public sealed class CalendarRateGate(TimeProvider timeProvider)
{
    private readonly object _lock = new();
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    /// <summary>Reserves the next send slot and returns how long the caller must wait before sending.</summary>
    public TimeSpan Reserve(int requestsPerMinute)
    {
        var rpm = Math.Max(1, requestsPerMinute);
        var minInterval = TimeSpan.FromSeconds(60.0 / rpm);
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            var start = now < _nextAllowed ? _nextAllowed : now;
            _nextAllowed = start + minInterval;
            return start - now;
        }
    }

    /// <summary>Backs the gate off by the server-requested cooldown after a 429.</summary>
    public void Penalize(TimeSpan retryAfter)
    {
        lock (_lock)
        {
            var candidate = timeProvider.GetUtcNow() + retryAfter;
            if (candidate > _nextAllowed) _nextAllowed = candidate;
        }
    }
}

/// <summary>
/// Delegating handler that throttles a calendar source's outbound requests through the shared
/// <see cref="CalendarRateGate"/> and, on a <c>429 Too Many Requests</c>, honours the server's
/// <c>Retry-After</c> by backing the gate off. Combined with the durable read-through cache (Postgres),
/// a rate-limited or dead source degrades to best-known data rather than hammering the origin.
/// </summary>
public sealed class CalendarRateLimitHandler(
    CalendarRateGate gate, IOptionsMonitor<AppOptions> options, TimeProvider timeProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var wait = gate.Reserve(options.CurrentValue.Calendar.FredRequestsPerMinute);
        if (wait > TimeSpan.Zero) await Task.Delay(wait, timeProvider, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if ((int)response.StatusCode == 429)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta
                             ?? (response.Headers.RetryAfter?.Date is { } date
                                 ? date - timeProvider.GetUtcNow()
                                 : options.CurrentValue.Calendar.RateLimitBackoff);
            gate.Penalize(retryAfter > TimeSpan.Zero ? retryAfter : options.CurrentValue.Calendar.RateLimitBackoff);
        }

        return response;
    }
}
