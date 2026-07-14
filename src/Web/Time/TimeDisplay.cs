using System.Globalization;

namespace Web.Time;

/// <summary>
/// Formatting helpers that render a UTC <see cref="DateTimeOffset"/> in the user's resolved zone using the
/// current UI culture. The single seam every page/dialog uses instead of <c>ToLocalTime()</c> (which would
/// bind to the <b>server</b> zone in Blazor Server). Wire/parse stays UTC + invariant elsewhere.
/// </summary>
public static class TimeDisplay
{
    /// <summary>Converts a UTC timestamp into the user's zone (value unchanged on the wire/DB).</summary>
    public static DateTimeOffset ToUserTime(this DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone);

    /// <summary>Formats a UTC timestamp in the user's zone with the current culture.</summary>
    public static string ToUserString(this DateTimeOffset value, TimeZoneInfo zone, string format = "g") =>
        TimeZoneInfo.ConvertTime(value, zone).ToString(format, CultureInfo.CurrentCulture);
}
