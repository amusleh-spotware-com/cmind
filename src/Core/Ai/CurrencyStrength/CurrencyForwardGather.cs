namespace Core.Ai.CurrencyStrength;

/// <summary>
/// The parsed result of the AI forward gather: per-currency forward trajectories plus any current-figure
/// gap-fill (only what the calendar lacked). Keyed by ISO code. An empty result means the gather failed or was
/// malformed — the caller keeps the calendar-only current ranking and skips the forward projection.
/// </summary>
public sealed record CurrencyForwardGather(
    IReadOnlyDictionary<string, CurrencyMacroInputs> GapFill,
    IReadOnlyList<CurrencyTrajectory> Trajectories)
{
    public static CurrencyForwardGather Empty { get; } =
        new(new Dictionary<string, CurrencyMacroInputs>(StringComparer.Ordinal), []);

    public bool HasForward => Trajectories.Count > 0;
}
