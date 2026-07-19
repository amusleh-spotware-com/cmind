using Core.Constants;
using Core.Domain;

namespace Core.Cot;

/// <summary>
/// Computes the COT index — where the current net position sits within its historical range, normalised to
/// 0..100. 100 = the most net-long the window has ever been, 0 = the most net-short. A widely used contrarian
/// gauge: readings near the extremes flag crowded speculator positioning. Pure domain logic, no side effects.
/// </summary>
public static class CotIndexCalculator
{
    /// <summary>Default lookback for the index, in weekly reports (~3 years).</summary>
    public const int DefaultLookbackWeeks = 156;

    /// <summary>The COT index reading (0..100) when the range collapses to a single value.</summary>
    public const double NeutralIndex = 50d;

    /// <summary>Below this the reading is a short extreme; above <see cref="HighBand"/> a long extreme.</summary>
    public const double LowBand = 20d;

    public const double HighBand = 80d;

    /// <summary>
    /// The COT index for the newest report in <paramref name="netsOldestToNewest"/> (its last element),
    /// measured against the min/max of the whole window. Requires at least two points; a flat window returns
    /// <see cref="NeutralIndex"/>.
    /// </summary>
    public static double Index(IReadOnlyList<long> netsOldestToNewest)
    {
        if (netsOldestToNewest.Count < 2)
            throw new DomainException(DomainErrors.CotHistoryInsufficient);

        var current = netsOldestToNewest[^1];
        var min = long.MaxValue;
        var max = long.MinValue;
        foreach (var net in netsOldestToNewest)
        {
            if (net < min) min = net;
            if (net > max) max = net;
        }

        if (max == min) return NeutralIndex;
        return 100d * (current - min) / (max - min);
    }

    /// <summary>Classifies a COT index reading into an extreme band.</summary>
    public static CotExtreme Classify(double index) => index switch
    {
        >= HighBand => CotExtreme.LongExtreme,
        <= LowBand => CotExtreme.ShortExtreme,
        _ => CotExtreme.None
    };
}
