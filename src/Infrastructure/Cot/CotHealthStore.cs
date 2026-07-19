using System.Text.Json;
using Core;
using Core.Cot;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Cot;

/// <summary>
/// Per-source freshness for the COT feed, persisted in app settings so it is visible across processes (the
/// worker records it, the reader/API surfaces it): last successful poll, consecutive-failure count, a tripped
/// circuit flag, and a staleness verdict a cBot or an agent can use to judge how far to trust the data.
/// </summary>
public sealed class CotHealthStore(DataContext db, IOptionsMonitor<AppOptions> options, TimeProvider timeProvider)
{
    private sealed record HealthState(
        DateTimeOffset? LastSuccessAt, DateTimeOffset? LastFailureAt, int ConsecutiveFailures, bool CircuitOpen);

    private static string Key(string source) => $"cot.source.{source.ToLowerInvariant()}.health";

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
                CircuitOpen = failures >= options.CurrentValue.Cot.CircuitFailureThreshold
            };
        }, ct);

    public async Task<IReadOnlyList<CotSourceHealth>> GetAllAsync(
        IReadOnlyList<string> sourceNames, CancellationToken ct)
    {
        var staleAfter = options.CurrentValue.Cot.SourceStaleAfter;
        var now = timeProvider.GetUtcNow();
        var result = new List<CotSourceHealth>(sourceNames.Count);

        foreach (var name in sourceNames)
        {
            var state = await ReadAsync(name, ct);
            var trackedMarkets = await db.CotMarkets.CountAsync(ct);
            var stale = state?.LastSuccessAt is not { } lastSuccess || now - lastSuccess > staleAfter;
            result.Add(new CotSourceHealth(name, state?.LastSuccessAt, state?.CircuitOpen ?? false, trackedMarkets, stale));
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
        var next = updated with { LastFailureAt = updated.LastFailureAt ?? previous.LastFailureAt };
        var json = JsonSerializer.Serialize(next);
        var now = timeProvider.GetUtcNow();

        if (setting is null) db.AppSettings.Add(AppSetting.Create(key, json, now));
        else setting.SetValue(json, now);

        await db.SaveChangesAsync(ct);
    }
}
