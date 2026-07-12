using System.Text.Json;
using Core.Ai.CurrencyStrength;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Ai.CurrencyStrength;

/// <summary>Shared JSON options for the currency-strength read model (case-insensitive, plain records).</summary>
internal static class CurrencyStrengthJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>EF-backed write/read of the raw snapshot aggregate.</summary>
public sealed class CurrencyStrengthSnapshots(DataContext db) : ICurrencyStrengthSnapshots
{
    public async Task AddAsync(CurrencyStrengthSnapshot snapshot, CancellationToken ct)
    {
        db.CurrencyStrengthSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);
    }

    public Task<CurrencyStrengthSnapshot?> LatestAsync(CancellationToken ct) =>
        db.CurrencyStrengthSnapshots.AsNoTracking()
            .OrderByDescending(s => s.AsOf)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CurrencyStrengthSnapshot>> SinceAsync(DateTimeOffset since, CancellationToken ct) =>
        await db.CurrencyStrengthSnapshots.AsNoTracking()
            .Where(s => s.AsOf >= since)
            .OrderBy(s => s.AsOf)
            .ToListAsync(ct);
}

/// <summary>
/// The single shared read model deserializing the latest snapshot into per-horizon rows. Backs the in-app AI
/// consumers, the MCP tool and the cBot REST endpoints alike.
/// </summary>
public sealed class CurrencyStrengthQuery(ICurrencyStrengthSnapshots snapshots) : ICurrencyStrengthQuery
{
    public async Task<CurrencyStrengthView?> LatestAsync(Horizon horizon, string? tierFilter, CancellationToken ct)
    {
        var snapshot = await snapshots.LatestAsync(ct);
        if (snapshot is null) return null;

        var ranking = Deserialize<List<RankRow>>(snapshot.RankingJson) ?? [];
        var horizons = Deserialize<Dictionary<string, HorizonLayer>>(snapshot.HorizonsJson) ?? [];
        var layer = horizons.GetValueOrDefault(horizon.Label()) ?? new HorizonLayer([], []);

        var tier = NormalizeTier(tierFilter);
        var rankRows = tier is null ? ranking : [.. ranking.Where(r => r.Tier == tier)];
        var codes = new HashSet<string>(rankRows.Select(r => r.Code), StringComparer.Ordinal);
        var forecasts = tier is null ? layer.Forecasts : [.. layer.Forecasts.Where(f => codes.Contains(f.Code))];
        var pairs = tier is null
            ? layer.Pairs
            : [.. layer.Pairs.Where(p => codes.Contains(p.Base) && codes.Contains(p.Quote))];

        return new CurrencyStrengthView(
            snapshot.AsOf, horizon.Label(), snapshot.Source.ToString(), snapshot.CalendarKnownAt,
            snapshot.Narrative, rankRows, forecasts, pairs);
    }

    public async Task<PairRow?> PairAsync(string @base, string quote, Horizon horizon, CancellationToken ct)
    {
        var view = await LatestAsync(horizon, null, ct);
        return view?.Pairs.FirstOrDefault(p =>
            string.Equals(p.Base, @base, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Quote, quote, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<StrengthHistoryPoint>> HistoryAsync(int days, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddDays(-Math.Clamp(days, 1, 3650));
        var rows = await snapshots.SinceAsync(since, ct);
        var points = new List<StrengthHistoryPoint>(rows.Count);
        foreach (var snapshot in rows)
        {
            var ranking = Deserialize<List<RankRow>>(snapshot.RankingJson) ?? [];
            points.Add(new StrengthHistoryPoint(
                snapshot.AsOf,
                ranking.ToDictionary(r => r.Code, r => r.Composite, StringComparer.Ordinal)));
        }

        return points;
    }

    private static string? NormalizeTier(string? tierFilter) => tierFilter?.Trim().ToUpperInvariant() switch
    {
        null or "" or "ALL" => null,
        "MAJOR" or "MAJORS" => nameof(CurrencyTier.Major),
        "EM" or "EMERGINGMARKET" or "EMERGING" => nameof(CurrencyTier.EmergingMarket),
        "EXOTIC" or "EXOTICS" => nameof(CurrencyTier.Exotic),
        _ => null
    };

    private static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, CurrencyStrengthJson.Options);
}
