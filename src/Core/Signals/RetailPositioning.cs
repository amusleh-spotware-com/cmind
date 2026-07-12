using Core.Constants;
using Core.Domain;

namespace Core.Signals;

/// <summary>The contrarian read on crowd positioning: fade the crowd when it is lopsided.</summary>
public enum ContrarianBias
{
    /// <summary>The crowd is heavily short → contrarian bullish.</summary>
    Bullish,

    /// <summary>Positioning is balanced → no contrarian signal.</summary>
    Neutral,

    /// <summary>The crowd is heavily long → contrarian bearish.</summary>
    Bearish
}

/// <summary>
/// Retail crowd positioning for an instrument and its contrarian interpretation. The retail crowd is a
/// well-documented contrarian indicator in FX: when more than ~60% are long, price tends to fall
/// (bearish), and below ~40% long it tends to rise (bullish); 40–60% is indecision. Self-validating.
/// </summary>
public readonly record struct RetailPositioning
{
    public const double LongThreshold = 60.0;
    public const double ShortThreshold = 40.0;

    public double LongPercent { get; }

    public RetailPositioning(double longPercent)
    {
        if (double.IsNaN(longPercent) || longPercent < 0.0 || longPercent > 100.0)
            throw new DomainException(DomainErrors.PositioningInvalid);
        LongPercent = longPercent;
    }

    public double ShortPercent => 100.0 - LongPercent;

    public ContrarianBias Bias =>
        LongPercent >= LongThreshold ? ContrarianBias.Bearish
        : LongPercent <= ShortThreshold ? ContrarianBias.Bullish
        : ContrarianBias.Neutral;

    /// <summary>How far into contrarian territory, 0 (balanced) to 1 (fully one-sided), for signal strength.</summary>
    public double Strength => Math.Abs(LongPercent - 50.0) / 50.0;
}
