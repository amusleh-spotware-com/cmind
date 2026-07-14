namespace Core.Constants;

/// <summary>
/// Constants for the per-user display time-zone mechanism: the cookie the time-zone switcher / detector
/// writes (read on every request to render times in the user's zone, mirroring the culture cookie).
/// </summary>
public static class TimeConstants
{
    /// <summary>Cookie holding the visitor's chosen/detected IANA time zone id.</summary>
    public const string TimeZoneCookieName = ".Cmind.TimeZone";

    /// <summary>Auth-cookie claim carrying the signed-in user's IANA time zone, read by the Blazor circuit
    /// (which has no <c>HttpContext</c> to read the cookie) to render times in the user's zone.</summary>
    public const string TimeZoneClaimType = "tz";
}
