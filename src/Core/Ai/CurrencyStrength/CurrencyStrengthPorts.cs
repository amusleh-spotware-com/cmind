namespace Core.Ai.CurrencyStrength;

/// <summary>Write/read of the raw snapshot aggregate (Infrastructure-backed).</summary>
public interface ICurrencyStrengthSnapshots
{
    Task AddAsync(CurrencyStrengthSnapshot snapshot, CancellationToken ct);
    Task<CurrencyStrengthSnapshot?> LatestAsync(CancellationToken ct);
    Task<IReadOnlyList<CurrencyStrengthSnapshot>> SinceAsync(DateTimeOffset since, CancellationToken ct);
}

/// <summary>
/// The single shared read model behind every consumer — the in-app AI features (DI), the MCP tool, and the
/// cBot REST endpoints — so read logic is defined once. Returns the deserialized Core read-model rows for the
/// requested horizon; a null result means no snapshot exists yet (empty state / degraded).
/// </summary>
public interface ICurrencyStrengthQuery
{
    Task<CurrencyStrengthView?> LatestAsync(Horizon horizon, string? tierFilter, CancellationToken ct);
    Task<PairRow?> PairAsync(string @base, string quote, Horizon horizon, CancellationToken ct);
    Task<IReadOnlyList<StrengthHistoryPoint>> HistoryAsync(int days, DateTimeOffset now, CancellationToken ct);
}
