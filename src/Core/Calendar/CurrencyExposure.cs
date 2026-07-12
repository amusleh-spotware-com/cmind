namespace Core.Calendar;

/// <summary>
/// Pure country → currency → symbol resolution — the single most-cited algo integration papercut, solved
/// once and shared. EURUSD is exposed to <em>both</em> EUR (EU) and USD (US) events; every euro-area member
/// (DE/FR/IT…) fans in to EUR. No external calls, fully deterministic, fully unit-tested.
/// </summary>
public static class CurrencyExposure
{
    // The freely-floating currencies we recognise inside a symbol. Metals (XAU…) are handled separately —
    // they are not a country's currency but their symbols are USD-exposed via the quote leg.
    private static readonly HashSet<string> KnownCurrencies = new(StringComparer.Ordinal)
    {
        "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD",
        "SEK", "NOK", "DKK", "PLN", "CZK", "HUF", "MXN", "ZAR",
        "SGD", "HKD", "CNH", "TRY", "ILS", "KRW"
    };

    private static readonly HashSet<string> Metals = new(StringComparer.Ordinal) { "XAU", "XAG", "XPT", "XPD" };

    // The euro-area members (and pseudo-codes) that all map to EUR.
    private static readonly HashSet<string> EuroArea = new(StringComparer.Ordinal)
    {
        "EU", "XM", "EA", "AT", "BE", "CY", "DE", "EE", "ES", "FI", "FR", "GR", "HR",
        "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PT", "SI", "SK"
    };

    private static readonly Dictionary<string, string> CountryToCurrency = new(StringComparer.Ordinal)
    {
        ["US"] = "USD", ["GB"] = "GBP", ["UK"] = "GBP", ["JP"] = "JPY", ["AU"] = "AUD",
        ["CA"] = "CAD", ["CH"] = "CHF", ["NZ"] = "NZD", ["SE"] = "SEK", ["NO"] = "NOK",
        ["DK"] = "DKK", ["PL"] = "PLN", ["CZ"] = "CZK", ["HU"] = "HUF", ["MX"] = "MXN",
        ["ZA"] = "ZAR", ["SG"] = "SGD", ["HK"] = "HKD", ["CN"] = "CNH", ["TR"] = "TRY",
        ["IL"] = "ILS", ["KR"] = "KRW"
    };

    // Common index-symbol prefixes → the currency they trade in.
    private static readonly Dictionary<string, string> IndexPrefixToCurrency = new(StringComparer.Ordinal)
    {
        ["US"] = "USD", ["NAS"] = "USD", ["SPX"] = "USD", ["DJ"] = "USD", ["WALL"] = "USD",
        ["UK"] = "GBP", ["FTSE"] = "GBP", ["DE"] = "EUR", ["GER"] = "EUR", ["FRA"] = "EUR",
        ["EU"] = "EUR", ["STOXX"] = "EUR", ["ESP"] = "EUR", ["IT"] = "EUR", ["JP"] = "JPY",
        ["JPN"] = "JPY", ["NIK"] = "JPY", ["AU"] = "AUD", ["AUS"] = "AUD", ["HK"] = "HKD",
        ["CH"] = "CHF", ["SWI"] = "CHF"
    };

    /// <summary>The currency (or currencies) a country's releases move — euro-area members all yield EUR.</summary>
    public static IReadOnlyCollection<CurrencyCode> CurrenciesOf(CountryCode country)
    {
        var code = country.Value;
        if (EuroArea.Contains(code)) return [new CurrencyCode("EUR")];
        return CountryToCurrency.TryGetValue(code, out var ccy) ? [new CurrencyCode(ccy)] : [];
    }

    /// <summary>The currencies a symbol is exposed to — both legs of an FX pair, the quote of a metal, or an index's currency.</summary>
    public static IReadOnlyCollection<CurrencyCode> CurrenciesOf(Symbol symbol)
    {
        var raw = Normalize(symbol.Value);
        var result = new List<CurrencyCode>();

        if (raw.Length == 6)
        {
            var left = raw[..3];
            var right = raw[3..];
            AddIfCurrency(result, left);
            AddIfCurrency(result, right);
            if (result.Count > 0) return result;
        }

        foreach (var (prefix, ccy) in IndexPrefixToCurrency)
            if (raw.StartsWith(prefix, StringComparison.Ordinal))
                return [new CurrencyCode(ccy)];

        // Fallback: an unrecognised metal or single-currency instrument still exposes any embedded currency.
        AddIfCurrency(result, raw.Length >= 3 ? raw[^3..] : raw);
        return result;
    }

    /// <summary>Symbols in the watchlist whose base/quote/index currency intersects the event country's currency.</summary>
    public static IReadOnlyList<Symbol> AffectedSymbols(CountryCode country, IEnumerable<Symbol> watchlist)
    {
        var countryCurrencies = new HashSet<string>(
            CurrenciesOf(country).Select(c => c.Value), StringComparer.Ordinal);
        if (countryCurrencies.Count == 0) return [];

        var affected = new List<Symbol>();
        foreach (var symbol in watchlist)
        {
            foreach (var ccy in CurrenciesOf(symbol))
            {
                if (!countryCurrencies.Contains(ccy.Value)) continue;
                affected.Add(symbol);
                break;
            }
        }

        return affected;
    }

    /// <summary>True when any of the event country's currencies is one the symbol is exposed to.</summary>
    public static bool Affects(CountryCode country, Symbol symbol)
    {
        var countryCurrencies = CurrenciesOf(country);
        if (countryCurrencies.Count == 0) return false;
        var symbolCurrencies = CurrenciesOf(symbol);
        foreach (var cc in countryCurrencies)
            foreach (var sc in symbolCurrencies)
                if (string.Equals(cc.Value, sc.Value, StringComparison.Ordinal))
                    return true;
        return false;
    }

    private static void AddIfCurrency(List<CurrencyCode> target, string candidate)
    {
        if (candidate.Length == 3 && KnownCurrencies.Contains(candidate) && !Metals.Contains(candidate))
            target.Add(new CurrencyCode(candidate));
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                buffer[length++] = char.ToUpperInvariant(c);
        }

        return new string(buffer[..length]);
    }
}
