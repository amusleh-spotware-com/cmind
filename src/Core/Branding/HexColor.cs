using System.Text.RegularExpressions;
using Core.Constants;
using Core.Domain;

namespace Core.Branding;

/// <summary>
/// A CSS hex colour (<c>#RGB</c> or <c>#RRGGBB</c>). Immutable, equality by value, validated at construction —
/// a branding colour can never carry an invalid value into the theme.
/// </summary>
public readonly partial record struct HexColor
{
    public string Value { get; }

    public HexColor(string value)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.BrandingColorInvalid);
        if (!Pattern().IsMatch(trimmed))
            throw new DomainException(DomainErrors.BrandingColorInvalid);
        Value = trimmed;
    }

    public static HexColor From(string value) => new(value);

    public override string ToString() => Value;

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex Pattern();
}
