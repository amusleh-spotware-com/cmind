using Core.Options;

namespace Core.Ai.CurrencyStrength;

/// <summary>
/// Builds the in-play <see cref="CurrencyUniverse"/> from deployment config, falling back to a curated default
/// (the 8 majors + a curated EM/exotic set) when config supplies none. Pure — adding a currency is config,
/// not code.
/// </summary>
public static class CurrencyUniverseFactory
{
    /// <summary>The curated default universe: majors + a representative EM/exotic set with peg flags.</summary>
    public static readonly IReadOnlyList<Currency> Default =
    [
        new("USD", CurrencyTier.Major), new("EUR", CurrencyTier.Major), new("GBP", CurrencyTier.Major),
        new("JPY", CurrencyTier.Major), new("AUD", CurrencyTier.Major), new("NZD", CurrencyTier.Major),
        new("CAD", CurrencyTier.Major), new("CHF", CurrencyTier.Major),
        new("NOK", CurrencyTier.EmergingMarket), new("SEK", CurrencyTier.EmergingMarket),
        new("CNH", CurrencyTier.EmergingMarket, isPegged: true, pegAnchor: "USD"),
        new("INR", CurrencyTier.EmergingMarket), new("BRL", CurrencyTier.EmergingMarket),
        new("MXN", CurrencyTier.EmergingMarket), new("ZAR", CurrencyTier.EmergingMarket),
        new("KRW", CurrencyTier.EmergingMarket), new("SGD", CurrencyTier.EmergingMarket),
        new("PLN", CurrencyTier.EmergingMarket),
        new("TRY", CurrencyTier.Exotic), new("HUF", CurrencyTier.Exotic), new("CZK", CurrencyTier.Exotic),
        new("HKD", CurrencyTier.Exotic, isPegged: true, pegAnchor: "USD"),
        new("SAR", CurrencyTier.Exotic, isPegged: true, pegAnchor: "USD")
    ];

    public static CurrencyUniverse FromConfig(CurrencyStrengthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Universe.Count == 0)
            return CurrencyUniverse.Of(Default);

        var currencies = options.Universe
            .Select(c => new Currency(c.Code, c.Tier, c.IsPegged, c.PegAnchor))
            .ToList();
        return CurrencyUniverse.Of(currencies);
    }
}
