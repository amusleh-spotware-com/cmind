using Core.Cot;

namespace IntegrationTests.Cot;

/// <summary>A deterministic <see cref="ICotSource"/> for tests — returns canned reports and counts fetches,
/// so the read-through cache can be exercised without touching the live CFTC endpoint.</summary>
public sealed class FakeCotSource : ICotSource
{
    public string Name => "CFTC";
    public int CallCount { get; private set; }
    public Func<CotReportKind, bool, IReadOnlyList<CotSourceReport>> Provider { get; set; } = (_, _) => [];

    public Task<IReadOnlyList<CotSourceReport>> FetchAsync(
        CotReportKind kind, bool combined, DateTimeOffset since,
        IReadOnlyCollection<string>? contractCodes, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(Provider(kind, combined));
    }
}
