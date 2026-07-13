namespace Core.Calendar;

/// <summary>
/// Inputs to the deterministic impact score — every value is known at scoring time, never a future leak.
/// <para><see cref="SeriesPrior"/>: baseline weight for the indicator class (rate decision ≫ CPI ≫ minor
/// survey), in [0, 1].</para>
/// <para><see cref="VolFootprint"/>: median absolute return of the primary affected symbols in the window
/// after this series' <em>past</em> releases (a price fraction, e.g. 0.004 = 40 bps).</para>
/// <para><see cref="SurpriseSensitivity"/>: how strongly |surprise| historically correlated with the
/// post-release move, in [0, 1].</para>
/// </summary>
public readonly record struct ImpactInputs(double SeriesPrior, double VolFootprint, double SurpriseSensitivity);

/// <summary>Score thresholds that band a 0–100 impact score into an <see cref="ImpactLevel"/>.</summary>
public sealed record ImpactBands(double MediumFrom, double HighFrom, double CriticalFrom)
{
    public static ImpactBands Default => new(25, 50, 75);

    public ImpactLevel Classify(ImpactScore score) => score.Value switch
    {
        var v when v >= CriticalFrom => ImpactLevel.Critical,
        var v when v >= HighFrom => ImpactLevel.High,
        var v when v >= MediumFrom => ImpactLevel.Medium,
        _ => ImpactLevel.Low
    };
}

/// <summary>The reproducible result of scoring an event: the raw score, its band, and the model version.</summary>
public readonly record struct ImpactAssessment(ImpactScore Score, ImpactLevel Level, int Version);

/// <summary>
/// Pure, deterministic, versioned impact scoring — the anti-"silent change" differentiator. Given the same
/// inputs it always yields the same score, so a user can be shown exactly <em>why</em> an event is High and
/// every change is an auditable recompute (a new revision), never a silent overwrite.
/// </summary>
public static class ImpactModel
{
    /// <summary>The model version stamped on every score; bump when the formula or weights change.</summary>
    public const int Version = 1;

    // Weights sum to 1; the score is a convex blend so it stays in [0, 100] and is monotonic in each input.
    private const double PriorWeight = 0.40;
    private const double VolWeight = 0.40;
    private const double SurpriseWeight = 0.20;

    // The realized-vol footprint at which the vol term saturates to its full weight (0.5% move = maxed out).
    private const double ReferenceFootprint = 0.005;

    public static ImpactAssessment Score(ImpactInputs inputs) => Score(inputs, ImpactBands.Default);

    public static ImpactAssessment Score(ImpactInputs inputs, ImpactBands bands)
    {
        var prior = Clamp01(inputs.SeriesPrior);
        var vol = Clamp01(inputs.VolFootprint / ReferenceFootprint);
        var sensitivity = Clamp01(inputs.SurpriseSensitivity);

        var raw = 100.0 * (PriorWeight * prior + VolWeight * vol + SurpriseWeight * sensitivity);
        var score = new ImpactScore(Clamp(raw, 0, 100));
        return new ImpactAssessment(score, bands.Classify(score), Version);
    }

    private static double Clamp01(double value) => Clamp(value, 0, 1);

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value)) return min;
        return value < min ? min : value > max ? max : value;
    }
}
