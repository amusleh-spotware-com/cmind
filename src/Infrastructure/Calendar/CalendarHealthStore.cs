using System.Text.Json;
using Core;
using Core.Calendar;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// Per-source freshness/coverage, persisted in app settings so it is visible across processes (the worker
/// records it, the reader/API surfaces it). Turns the health view from a stub into a real signal: last
/// successful poll, consecutive-failure count, a tripped-circuit flag, and a staleness verdict a cBot or an
/// agent can use to decide how far to trust the data.
/// </summary>
public sealed class CalendarHealthStore(DataContext db, IOptionsMonitor<AppOptions> options, TimeProvider timeProvider)
{
    private sealed record HealthState(
        DateTimeOffset? LastSuccessAt, DateTimeOffset? LastFailureAt, int ConsecutiveFailures, bool CircuitOpen);

    private static string Key(string source) => $"calendar.source.{source.ToLowerInvariant()}.health";

    public Task RecordSuccessAsync(string source, CancellationToken ct) =>
        UpsertAsync(source, _ => new HealthState(timeProvider.GetUtcNow(), null, 0, false), ct);

    public Task RecordFailureAsync(string source, CancellationToken ct) =>
        UpsertAsync(source, prev =>
        {
            var failures = prev.ConsecutiveFailures + 1;
            return prev with
            {
                LastFailureAt = timeProvider.GetUtcNow(),
                ConsecutiveFailures = failures,
                CircuitOpen = failures >= options.CurrentValue.Calendar.CircuitFailureThreshold
            };
        }, ct);

    public async Task<IReadOnlyList<SourceHealth>> GetAllAsync(IReadOnlyList<string> sourceNames, CancellationToken ct)
    {
        var staleAfter = options.CurrentValue.Calendar.SourceStaleAfter;
        var now = timeProvider.GetUtcNow();
        var result = new List<SourceHealth>(sourceNames.Count);

        foreach (var name in sourceNames)
        {
            var state = await ReadAsync(name, ct);
            var trackedSeries = await db.CalendarSeries.CountAsync(s => s.SourceName == name, ct);
            var stale = state?.LastSuccessAt is not { } lastSuccess || now - lastSuccess > staleAfter;
            result.Add(new SourceHealth(name, state?.LastSuccessAt, state?.CircuitOpen ?? false, trackedSeries, stale));
        }

        return result;
    }

    private async Task<HealthState?> ReadAsync(string source, CancellationToken ct)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == Key(source), ct);
        return setting is null ? null : JsonSerializer.Deserialize<HealthState>(setting.Value);
    }

    private async Task UpsertAsync(string source, Func<HealthState, HealthState> update, CancellationToken ct)
    {
        var key = Key(source);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        var previous = setting is null
            ? new HealthState(null, null, 0, false)
            : JsonSerializer.Deserialize<HealthState>(setting.Value) ?? new HealthState(null, null, 0, false);

        var updated = update(previous);
        // A success carries no failure time of its own — keep the last recorded one for context.
        var next = updated with { LastFailureAt = updated.LastFailureAt ?? previous.LastFailureAt };
        var json = JsonSerializer.Serialize(next);
        var now = timeProvider.GetUtcNow();

        if (setting is null) db.AppSettings.Add(AppSetting.Create(key, json, now));
        else setting.SetValue(json, now);

        await db.SaveChangesAsync(ct);
    }
}
