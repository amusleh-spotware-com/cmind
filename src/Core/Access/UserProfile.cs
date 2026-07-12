using System.Globalization;
using System.Text.RegularExpressions;
using Core.Constants;
using Core.Domain;

namespace Core;

/// <summary>
/// Optional, white-label-configurable user attributes, owned by <see cref="AppUser"/>. Every field is
/// nullable and validated at construction so a malformed value can never enter the model; an all-empty
/// profile (the default deployment, email-only) is valid. Which of these the registration form actually
/// collects — and whether each is required — is governed by <c>App:Registration:Attributes</c>, not here.
/// </summary>
public sealed partial record UserProfile
{
    public string? FullName { get; init; }
    public string? DisplayName { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code, upper-cased.</summary>
    public string? CountryCode { get; init; }

    /// <summary>E.164 phone number (e.g. <c>+14155552671</c>).</summary>
    public string? PhoneNumber { get; init; }

    public string? Company { get; init; }

    /// <summary>BCP-47 / .NET culture name (e.g. <c>en-US</c>).</summary>
    public string? Locale { get; init; }

    public bool MarketingOptIn { get; init; }
    public bool AgeConfirmed { get; init; }

    public static UserProfile Empty { get; } = new();

    /// <summary>
    /// Builds a validated profile. Blank strings become <c>null</c>; country/phone/locale are checked against
    /// their real formats (country + locale via the BCL culture/region tables). Throws
    /// <see cref="DomainException"/> on a malformed value.
    /// </summary>
    public static UserProfile Create(
        string? fullName = null, string? displayName = null, string? countryCode = null,
        string? phoneNumber = null, string? company = null, string? locale = null,
        bool marketingOptIn = false, bool ageConfirmed = false)
        => new()
        {
            FullName = Text(fullName),
            DisplayName = Text(displayName),
            Company = Text(company),
            CountryCode = Country(countryCode),
            PhoneNumber = Phone(phoneNumber),
            Locale = LocaleName(locale),
            MarketingOptIn = marketingOptIn,
            AgeConfirmed = ageConfirmed
        };

    private static string? Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > RegistrationConstants.MaxProfileTextLength)
            throw new DomainException(DomainErrors.ProfileTextTooLong);
        return trimmed;
    }

    private static string? Country(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var code = value.Trim().ToUpperInvariant();
        try
        {
            _ = new RegionInfo(code);
        }
        catch (ArgumentException)
        {
            throw new DomainException(DomainErrors.ProfileCountryInvalid);
        }
        if (code.Length != 2) throw new DomainException(DomainErrors.ProfileCountryInvalid);
        return code;
    }

    private static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!E164().IsMatch(trimmed)) throw new DomainException(DomainErrors.ProfilePhoneInvalid);
        return trimmed;
    }

    private static string? LocaleName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        try
        {
            return CultureInfo.GetCultureInfo(trimmed).Name;
        }
        catch (CultureNotFoundException)
        {
            throw new DomainException(DomainErrors.ProfileLocaleInvalid);
        }
    }

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex E164();
}
