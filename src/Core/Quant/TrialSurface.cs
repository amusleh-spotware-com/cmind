using Core.Constants;
using Core.Domain;

namespace Core.Quant;

/// <summary>
/// The full result surface of an optimization: one aligned return series per parameter set tried
/// (columns), over the same observations (rows). This is what the Probability of Backtest Overfitting
/// (Combinatorially-Symmetric Cross-Validation) operates on — it judges the *selection process*, not
/// just the single winner. Requires at least two trials of equal, non-trivial length.
/// </summary>
public sealed class TrialSurface
{
    private TrialSurface(IReadOnlyList<ReturnSeries> trials)
    {
        Trials = trials;
        Count = trials.Count;
        Observations = trials[0].Count;
    }

    public IReadOnlyList<ReturnSeries> Trials { get; }
    public int Count { get; }
    public int Observations { get; }

    public static TrialSurface From(IReadOnlyList<ReturnSeries> trials)
    {
        ArgumentNullException.ThrowIfNull(trials);
        if (trials.Count < 2) throw new DomainException(DomainErrors.TrialSurfaceInvalid);
        var length = trials[0].Count;
        if (length < 4 || trials.Any(t => t.Count != length))
            throw new DomainException(DomainErrors.TrialSurfaceInvalid);
        return new TrialSurface(trials);
    }
}
