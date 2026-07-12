using Core.Constants;
using Core.Domain;

namespace Core.Calendar;

/// <summary>
/// A stable dotted identifier for a tracked indicator, e.g. <c>US.CPI.MoM</c>. Upper-cased and validated at
/// construction so a series code can never carry whitespace or empty segments into the calendar.
/// </summary>
public readonly record struct SeriesCode
{
    public string Value { get; }

    public SeriesCode(string value)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.CalendarSeriesCodeRequired);
        Value = trimmed.ToUpperInvariant();
    }

    public static SeriesCode From(string value) => new(value);
    public override string ToString() => Value;
}

/// <summary>
/// An ISO-3166 alpha-2 country code, plus the pseudo-codes <c>EU</c>/<c>XM</c> for the euro area. Two letters,
/// upper-cased. The country is what maps to a currency (and thence to affected symbols).
/// </summary>
public readonly record struct CountryCode
{
    public string Value { get; }

    public CountryCode(string value)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.CalendarCountryCodeInvalid);
        trimmed = trimmed.ToUpperInvariant();
        if (trimmed.Length != 2 || !IsAllLetters(trimmed))
            throw new DomainException(DomainErrors.CalendarCountryCodeInvalid);
        Value = trimmed;
    }

    private static bool IsAllLetters(string value)
    {
        foreach (var c in value)
            if (c is < 'A' or > 'Z')
                return false;
        return true;
    }

    public static CountryCode From(string value) => new(value);
    public override string ToString() => Value;
}

/// <summary>An ISO-4217 alpha-3 currency code (or a 3-letter metal code such as XAU), upper-cased.</summary>
public readonly record struct CurrencyCode
{
    public string Value { get; }

    public CurrencyCode(string value)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.CalendarCurrencyCodeInvalid);
        trimmed = trimmed.ToUpperInvariant();
        if (trimmed.Length != 3)
            throw new DomainException(DomainErrors.CalendarCurrencyCodeInvalid);
        Value = trimmed;
    }

    public static CurrencyCode From(string value) => new(value);
    public override string ToString() => Value;
}

/// <summary>A deterministic impact score in the inclusive range [0, 100], range-checked at construction.</summary>
public readonly record struct ImpactScore
{
    public double Value { get; }

    public ImpactScore(double value)
    {
        if (double.IsNaN(value) || value is < 0 or > 100)
            throw new DomainException(DomainErrors.CalendarImpactScoreOutOfRange);
        Value = value;
    }

    public static ImpactScore Zero => new(0);
    public override string ToString() => Value.ToString("0.##");
}

/// <summary>
/// The surprise of a print: <c>(actual − forecast)</c> normalised by the series' rolling standard deviation,
/// i.e. a z-score. Positive = beat, negative = miss. Undefined (returns 0 z) when the stdev is not positive
/// or no forecast exists.
/// </summary>
public readonly record struct Surprise
{
    public decimal? Actual { get; }
    public decimal? Forecast { get; }
    public double ZScore { get; }

    private Surprise(decimal? actual, decimal? forecast, double zScore)
    {
        Actual = actual;
        Forecast = forecast;
        ZScore = zScore;
    }

    public static Surprise None => new(null, null, 0);

    /// <summary>Computes the surprise z-score; a non-positive <paramref name="rollingStdDev"/> yields z = 0.</summary>
    public static Surprise From(decimal? actual, decimal? forecast, double rollingStdDev)
    {
        if (actual is not { } a || forecast is not { } f || rollingStdDev <= 0)
            return new Surprise(actual, forecast, 0);
        var z = (double)(a - f) / rollingStdDev;
        return new Surprise(actual, forecast, z);
    }

    public bool IsBeat => ZScore > 0;
    public bool IsMiss => ZScore < 0;
    public override string ToString() => $"{ZScore:+0.00;-0.00;0.00}σ";
}

/// <summary>
/// A scheduled release instant plus how precisely it is known. The instant is always a UTC anchor; the
/// source's own timezone is stored alongside on the event so per-user rendering stays deterministic.
/// </summary>
public readonly record struct ReleaseWindow
{
    public DateTimeOffset Instant { get; }
    public ReleasePrecision Precision { get; }

    public ReleaseWindow(DateTimeOffset instant, ReleasePrecision precision)
    {
        Instant = instant.ToUniversalTime();
        Precision = precision;
    }

    public static ReleaseWindow Exact(DateTimeOffset instant) => new(instant, ReleasePrecision.Exact);
    public static ReleaseWindow OnDay(DateTimeOffset instant) => new(instant, ReleasePrecision.Day);
    public static ReleaseWindow Tentative(DateTimeOffset instant) => new(instant, ReleasePrecision.Tentative);
}
