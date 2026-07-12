using Core.Constants;
using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>
/// The reliability class of a currency's data and how the model treats it. Majors have deep, high-frequency,
/// revision-tracked official data; EM/exotics lean on additional drivers (carry, external vulnerability) and
/// lower-frequency, sometimes opaque figures — so the model widens their dead-band and caps conviction.
/// </summary>
public enum CurrencyTier
{
    Major,
    EmergingMarket,
    Exotic
}

/// <summary>
/// A currency in the configured universe — an ISO-4217 alpha-3 code paired with its <see cref="CurrencyTier"/>
/// and peg metadata. This is <em>not</em> a fixed set of 8: the deployment universe defines which codes and
/// tiers are in play, and the calculators operate over whatever the <see cref="CurrencyUniverse"/> holds.
/// </summary>
public readonly record struct Currency
{
    public string Code { get; }
    public CurrencyTier Tier { get; }

    /// <summary>True for a USD-pegged or heavily-managed regime (HKD/AED/SAR pegs, CNH management); the
    /// forward engine down-weights its trajectory and clamps its pair outlook toward <c>Neutral</c>.</summary>
    public bool IsPegged { get; }

    /// <summary>The currency this one is pegged/anchored to (e.g. USD for HKD), or null when free-floating.</summary>
    public string? PegAnchor { get; }

    public Currency(string code, CurrencyTier tier, bool isPegged = false, string? pegAnchor = null)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(code, DomainErrors.CalendarCurrencyCodeInvalid).ToUpperInvariant();
        if (trimmed.Length != 3)
            throw new DomainException(DomainErrors.CalendarCurrencyCodeInvalid);
        Code = trimmed;
        Tier = tier;
        IsPegged = isPegged;
        PegAnchor = string.IsNullOrWhiteSpace(pegAnchor) ? null : pegAnchor.Trim().ToUpperInvariant();
    }

    public override string ToString() => Code;
}

/// <summary>
/// The ordered set of currencies in play for one computation, built from deployment config. A universe is
/// non-empty and has no duplicate codes; membership + tier/peg lookup is by code. Majors-only and
/// majors+EM+exotics are the exact same code path — the engine just sees a different N.
/// </summary>
public sealed class CurrencyUniverse
{
    private readonly Dictionary<string, Currency> _byCode;

    public IReadOnlyList<Currency> Currencies { get; }

    private CurrencyUniverse(IReadOnlyList<Currency> currencies, Dictionary<string, Currency> byCode)
    {
        Currencies = currencies;
        _byCode = byCode;
    }

    public static CurrencyUniverse Of(IReadOnlyList<Currency> currencies)
    {
        ArgumentNullException.ThrowIfNull(currencies);
        if (currencies.Count == 0)
            throw new DomainException(DomainErrors.CurrencyUniverseEmpty);

        var byCode = new Dictionary<string, Currency>(StringComparer.Ordinal);
        foreach (var currency in currencies)
        {
            if (!byCode.TryAdd(currency.Code, currency))
                throw new DomainException(DomainErrors.CurrencyUniverseDuplicate);
        }

        return new CurrencyUniverse([.. currencies], byCode);
    }

    public bool Contains(string code) => _byCode.ContainsKey(code.ToUpperInvariant());

    /// <summary>Resolves a code to its configured <see cref="Currency"/>; unknown code ⇒ <see cref="DomainException"/>.</summary>
    public Currency Resolve(string code)
    {
        if (!_byCode.TryGetValue(code.ToUpperInvariant(), out var currency))
            throw new DomainException(DomainErrors.CurrencyNotInUniverse);
        return currency;
    }

    public IReadOnlyList<Currency> OfTier(CurrencyTier tier) =>
        [.. Currencies.Where(c => c.Tier == tier)];
}
