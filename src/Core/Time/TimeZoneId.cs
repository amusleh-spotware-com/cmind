using Core.Constants;
using Core.Domain;

namespace Core.Time;

/// <summary>
/// A validated IANA time zone (e.g. <c>Europe/London</c>, <c>America/New_York</c>) that a user's displayed
/// times are rendered in. Wrapping the raw id in a value object means an unsupported or malformed zone can
/// never reach the user's profile or the time-zone cookie — it is rejected at construction with a
/// <see cref="DomainException"/>, exactly like <see cref="Localization.CultureName"/>. The stored value is
/// always the canonical IANA id (a Windows id such as <c>GMT Standard Time</c> is normalized on the way in)
/// so the same value resolves identically on Windows and Linux.
/// </summary>
public readonly record struct TimeZoneId
{
    /// <summary>The coordinated-universal-time zone — the safe default when nothing else is known.</summary>
    public static TimeZoneId Utc { get; } = new("UTC");

    public string Value { get; }

    private TimeZoneId(string value) => Value = value;

    /// <summary>
    /// Builds a <see cref="TimeZoneId"/> from a raw zone string. Accepts either an IANA id or a Windows id
    /// and stores the canonical IANA form. Throws <see cref="DomainException"/> when the zone is unknown.
    /// </summary>
    public static TimeZoneId From(string? value)
    {
        if (!TryFrom(value, out var zone))
            throw new DomainException(DomainErrors.TimeZoneNotSupported);
        return zone;
    }

    /// <summary>Non-throwing parse. Returns <c>false</c> for null/blank/unknown input.</summary>
    public static bool TryFrom(string? value, out TimeZoneId zone)
    {
        zone = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!TryResolve(value.Trim(), out var info)) return false;

        zone = new TimeZoneId(Canonicalize(info));
        return true;
    }

    /// <summary>Resolves this zone to a <see cref="TimeZoneInfo"/> for converting/formatting times.</summary>
    public TimeZoneInfo ToTimeZoneInfo() =>
        TryResolve(Value, out var info) ? info : TimeZoneInfo.Utc;

    // Resolve an IANA *or* Windows id to a TimeZoneInfo regardless of the host's zone database. A Windows-only
    // host (no ICU IANA data) can't find "America/New_York" directly, so we also try the id converted to the
    // other family; likewise an IANA-only host (Linux) can't find "GMT Standard Time" directly.
    private static bool TryResolve(string id, out TimeZoneInfo info)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out info!)) return true;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windows)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windows, out info!)) return true;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana)
            && TimeZoneInfo.TryFindSystemTimeZoneById(iana, out info!)) return true;
        info = null!;
        return false;
    }

    // Prefer the IANA id so the persisted value is portable across OSes. On a system that resolved a Windows
    // id, convert it; if no IANA mapping exists, fall back to whatever id the platform gave us.
    private static string Canonicalize(TimeZoneInfo info)
    {
        if (info.HasIanaId) return info.Id;
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(info.Id, out var iana) ? iana : info.Id;
    }

    public override string ToString() => Value;
}
