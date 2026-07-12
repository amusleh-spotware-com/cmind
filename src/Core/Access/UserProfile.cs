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
        if (!Iso3166Alpha2.Contains(code)) throw new DomainException(DomainErrors.ProfileCountryInvalid);
        return code;
    }

    private static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!E164().IsMatch(trimmed)) throw new DomainException(DomainErrors.ProfilePhoneInvalid);
        return trimmed;
    }

    // Validates a BCP-47-style locale (language[-region][-...]) by shape — deterministic across globalization
    // settings — and normalizes it: language lower-cased, a two-letter region upper-cased (e.g. en-us -> en-US).
    private static string? LocaleName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!LocalePattern().IsMatch(trimmed)) throw new DomainException(DomainErrors.ProfileLocaleInvalid);

        var parts = trimmed.Split('-');
        parts[0] = parts[0].ToLowerInvariant();
        for (var i = 1; i < parts.Length; i++)
            if (parts[i].Length == 2 && parts[i].All(char.IsLetter))
                parts[i] = parts[i].ToUpperInvariant();
        return string.Join('-', parts);
    }

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex E164();

    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LocalePattern();

    // ISO 3166-1 alpha-2 country codes. A fixed, deterministic set — no dependency on ICU / globalization mode.
    private static readonly HashSet<string> Iso3166Alpha2 = new(StringComparer.Ordinal)
    {
        "AD","AE","AF","AG","AI","AL","AM","AO","AQ","AR","AS","AT","AU","AW","AX","AZ","BA","BB","BD","BE",
        "BF","BG","BH","BI","BJ","BL","BM","BN","BO","BQ","BR","BS","BT","BV","BW","BY","BZ","CA","CC","CD",
        "CF","CG","CH","CI","CK","CL","CM","CN","CO","CR","CU","CV","CW","CX","CY","CZ","DE","DJ","DK","DM",
        "DO","DZ","EC","EE","EG","EH","ER","ES","ET","FI","FJ","FK","FM","FO","FR","GA","GB","GD","GE","GF",
        "GG","GH","GI","GL","GM","GN","GP","GQ","GR","GS","GT","GU","GW","GY","HK","HM","HN","HR","HT","HU",
        "ID","IE","IL","IM","IN","IO","IQ","IR","IS","IT","JE","JM","JO","JP","KE","KG","KH","KI","KM","KN",
        "KP","KR","KW","KY","KZ","LA","LB","LC","LI","LK","LR","LS","LT","LU","LV","LY","MA","MC","MD","ME",
        "MF","MG","MH","MK","ML","MM","MN","MO","MP","MQ","MR","MS","MT","MU","MV","MW","MX","MY","MZ","NA",
        "NC","NE","NF","NG","NI","NL","NO","NP","NR","NU","NZ","OM","PA","PE","PF","PG","PH","PK","PL","PM",
        "PN","PR","PS","PT","PW","PY","QA","RE","RO","RS","RU","RW","SA","SB","SC","SD","SE","SG","SH","SI",
        "SJ","SK","SL","SM","SN","SO","SR","SS","ST","SV","SX","SY","SZ","TC","TD","TF","TG","TH","TJ","TK",
        "TL","TM","TN","TO","TR","TT","TV","TW","TZ","UA","UG","UM","US","UY","UZ","VA","VC","VE","VG","VI",
        "VN","VU","WF","WS","YE","YT","ZA","ZM","ZW"
    };
}
