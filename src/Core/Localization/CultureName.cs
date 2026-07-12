using Core.Constants;
using Core.Domain;

namespace Core.Localization;

/// <summary>
/// A validated UI language: one of the cultures the app actually localizes into
/// (<see cref="SupportedCultures.All"/>). Wrapping the raw BCP-47 string in a value object means an
/// unsupported or malformed culture can never reach the user's profile or the culture cookie — it is
/// rejected at construction with a <see cref="DomainException"/>, exactly like every other Core concept.
/// </summary>
public readonly record struct CultureName
{
    public string Value { get; }

    private CultureName(string value) => Value = value;

    /// <summary>True when this language is written right-to-left (drives <c>dir="rtl"</c> + MudBlazor).</summary>
    public bool IsRightToLeft => SupportedCultures.IsRightToLeft(Value);

    /// <summary>
    /// Builds a <see cref="CultureName"/> from a raw culture string, trimming it and matching it
    /// case-insensitively against the supported set (so <c>PT-br</c> normalizes to <c>pt-BR</c>).
    /// Throws <see cref="DomainException"/> when the culture is not one the app supports.
    /// </summary>
    public static CultureName From(string? value)
    {
        if (!TryFrom(value, out var culture))
            throw new DomainException(DomainErrors.CultureNotSupported);
        return culture;
    }

    /// <summary>Non-throwing parse. Returns <c>false</c> for null/blank/unsupported input.</summary>
    public static bool TryFrom(string? value, out CultureName culture)
    {
        culture = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        foreach (var supported in SupportedCultures.All)
        {
            if (string.Equals(supported, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                culture = new CultureName(supported);
                return true;
            }
        }
        return false;
    }

    public override string ToString() => Value;
}
