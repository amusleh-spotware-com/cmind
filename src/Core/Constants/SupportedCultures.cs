using System.Collections.Frozen;

namespace Core.Constants;

/// <summary>
/// The single source of truth for the languages the app localizes into — deliberately mirroring the
/// interface languages cTrader itself ships (Spotware: cTrader Web/Desktop/Mobile in 23 languages), so a
/// cTrader trader finds their language here too. Every localization surface — the request-culture
/// middleware, the language switcher, the resource-parity test, and the no-hardcoded-string gate — reads
/// this list; nothing hard-codes a culture elsewhere.
/// </summary>
public static class SupportedCultures
{
    /// <summary>Invariant fallback culture. Every missing translation resolves up the chain to this.</summary>
    public const string Default = "en";

    /// <summary>
    /// BCP-47 / .NET culture names, in display order (English first, then alphabetical by English name).
    /// Keep in lock-step with the cTrader language set; adding one here is all the runtime needs.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        "en",       // English
        "ar",       // Arabic (RTL)
        "cs",       // Czech
        "de",       // German
        "el",       // Greek
        "es",       // Spanish
        "fr",       // French
        "hu",       // Hungarian
        "id",       // Indonesian
        "it",       // Italian
        "ja",       // Japanese
        "ko",       // Korean
        "ms",       // Malay
        "pl",       // Polish
        "pt-BR",    // Portuguese (Brazil)
        "ru",       // Russian
        "sk",       // Slovak
        "sl",       // Slovenian
        "sr",       // Serbian
        "th",       // Thai
        "tr",       // Turkish
        "vi",       // Vietnamese
        "zh-Hans"   // Chinese (Simplified)
    ];

    /// <summary>Endonym (name of the language in that language) shown in the language switcher.</summary>
    public static readonly IReadOnlyDictionary<string, string> NativeNames = new Dictionary<string, string>
    {
        ["en"] = "English",
        ["ar"] = "العربية",
        ["cs"] = "Čeština",
        ["de"] = "Deutsch",
        ["el"] = "Ελληνικά",
        ["es"] = "Español",
        ["fr"] = "Français",
        ["hu"] = "Magyar",
        ["id"] = "Bahasa Indonesia",
        ["it"] = "Italiano",
        ["ja"] = "日本語",
        ["ko"] = "한국어",
        ["ms"] = "Bahasa Melayu",
        ["pl"] = "Polski",
        ["pt-BR"] = "Português (Brasil)",
        ["ru"] = "Русский",
        ["sk"] = "Slovenčina",
        ["sl"] = "Slovenščina",
        ["sr"] = "Српски",
        ["th"] = "ไทย",
        ["tr"] = "Türkçe",
        ["vi"] = "Tiếng Việt",
        ["zh-Hans"] = "简体中文"
    }.ToFrozenDictionary();

    // Right-to-left scripts among the supported set. Kept as a set (not an Arabic-only check) so a future
    // RTL addition — Persian (fa), Hebrew (he), Urdu (ur) — flips the layout the moment it joins All.
    private static readonly FrozenSet<string> RightToLeftLanguages =
        new[] { "ar", "fa", "he", "ur" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> Supported =
        All.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="culture"/> is one the app localizes into (exact match).</summary>
    public static bool IsSupported(string? culture) =>
        !string.IsNullOrWhiteSpace(culture) && Supported.Contains(culture);

    /// <summary>
    /// True when the culture's primary language is written right-to-left. Accepts a full culture name
    /// (<c>ar</c>, <c>ar-SA</c>) — only the language subtag is inspected.
    /// </summary>
    public static bool IsRightToLeft(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return false;
        var language = culture.Split('-', 2)[0];
        return RightToLeftLanguages.Contains(language);
    }

    /// <summary>The <c>dir</c> attribute value (<c>rtl</c>/<c>ltr</c>) for a culture.</summary>
    public static string Direction(string? culture) => IsRightToLeft(culture) ? "rtl" : "ltr";
}
