namespace Core.Constants;

/// <summary>
/// The list of time zones offered in the picker. Sourced from the platform's own IANA zone database
/// (<see cref="TimeZoneInfo.GetSystemTimeZones"/>), canonicalized to IANA ids and ordered by UTC offset,
/// so a user can pick any real zone. Validation is not limited to this list — <see cref="Time.TimeZoneId"/>
/// accepts any zone the platform knows — but this curated, ordered set is what the UI enumerates.
/// </summary>
public static class SupportedTimeZones
{
    /// <summary>Safe default zone when the user has not chosen and the browser was not detected.</summary>
    public const string Default = "UTC";

    /// <summary>A selectable zone: its canonical IANA id plus a human display label.</summary>
    public readonly record struct TimeZoneOption(string Id, string DisplayName);

    /// <summary>Every offerable zone, ordered by UTC offset then id.</summary>
    public static IReadOnlyList<TimeZoneOption> All { get; } = Build();

    private static IReadOnlyList<TimeZoneOption> Build()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var options = new List<(TimeSpan Offset, TimeZoneOption Option)>();

        foreach (var info in TimeZoneInfo.GetSystemTimeZones())
        {
            var id = info.HasIanaId
                ? info.Id
                : TimeZoneInfo.TryConvertWindowsIdToIanaId(info.Id, out var iana) ? iana : info.Id;

            if (!seen.Add(id)) continue;
            options.Add((info.BaseUtcOffset, new TimeZoneOption(id, info.DisplayName)));
        }

        // UTC is always offerable even on a stripped-down zone database.
        if (seen.Add("UTC"))
            options.Add((TimeSpan.Zero, new TimeZoneOption("UTC", "(UTC) Coordinated Universal Time")));

        return [.. options.OrderBy(o => o.Offset).ThenBy(o => o.Option.Id, StringComparer.Ordinal)
            .Select(o => o.Option)];
    }
}
