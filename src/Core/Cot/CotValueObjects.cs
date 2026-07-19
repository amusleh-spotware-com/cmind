using Core.Constants;
using Core.Domain;

namespace Core.Cot;

/// <summary>
/// A CFTC contract market code — the stable 6-digit identifier of a contract market (e.g. <c>099741</c> for
/// EUR FX on the CME). Trimmed and validated at construction so it can never carry whitespace into the store.
/// </summary>
public readonly record struct ContractMarketCode
{
    public string Value { get; }

    public ContractMarketCode(string value)
    {
        Value = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.CotContractCodeRequired);
    }

    public static ContractMarketCode From(string value) => new(value);
    public override string ToString() => Value;
}

/// <summary>
/// Identifies one of the six published report variants: a <see cref="CotReportKind"/> in either its
/// futures-only or futures-and-options-combined form.
/// </summary>
public readonly record struct CotReportType
{
    public CotReportKind Kind { get; }

    /// <summary>Futures-and-options combined when <c>true</c>; futures-only when <c>false</c>.</summary>
    public bool Combined { get; }

    public CotReportType(CotReportKind kind, bool combined)
    {
        Kind = kind;
        Combined = combined;
    }

    public static CotReportType FuturesOnly(CotReportKind kind) => new(kind, false);
    public static CotReportType CombinedWith(CotReportKind kind) => new(kind, true);

    public override string ToString() => $"{Kind}/{(Combined ? "Combined" : "FuturesOnly")}";
}

/// <summary>
/// A long/short/spread position triple for one trader category. <see cref="Net"/> is the directional
/// gauge (long − short); spread positions are market-neutral and excluded from net. Values are open-interest
/// contract counts and can never be negative.
/// </summary>
public readonly record struct CotPositions
{
    public long Long { get; }
    public long Short { get; }
    public long Spread { get; }

    public CotPositions(long longs, long shorts, long spread)
    {
        if (longs < 0 || shorts < 0 || spread < 0)
            throw new DomainException(DomainErrors.CotPositionNegative);
        Long = longs;
        Short = shorts;
        Spread = spread;
    }

    public long Net => Long - Short;

    /// <summary>Long positions as a percent of total open interest, 0 when open interest is not positive.</summary>
    public double LongPercentOf(long openInterest) => openInterest > 0 ? 100d * Long / openInterest : 0d;

    public double ShortPercentOf(long openInterest) => openInterest > 0 ? 100d * Short / openInterest : 0d;
}
