using Core.Calendar;

namespace Infrastructure.Calendar;

/// <summary>
/// Central-bank rate-decision schedule — the forward meeting calendars the banks publish (Fed/FOMC, ECB,
/// BoE), which the value-series sources (FRED/BLS) do not carry. It answers the schedule side only: the
/// announcement instants within a window, as <see cref="ReleaseWindow.Exact"/>. The printed decision (the new
/// rate) arrives later via the value sources. Dates are the officially published decision days; extend as new
/// years are announced.
/// </summary>
public sealed class CentralBankScheduleSource : ICalendarSource
{
    public string Name => "CentralBankSchedule";

    // Announcement instants in UTC (Fed ~19:00, ECB ~12:15, BoE ~12:00). Officially published decision days.
    private static readonly Dictionary<string, DateTimeOffset[]> Meetings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FOMC"] = Utc(19, 0,
                (2025, 1, 29), (2025, 3, 19), (2025, 5, 7), (2025, 6, 18), (2025, 7, 30), (2025, 9, 17),
                (2025, 10, 29), (2025, 12, 10),
                (2026, 1, 28), (2026, 3, 18), (2026, 4, 29), (2026, 6, 17), (2026, 7, 29), (2026, 9, 16),
                (2026, 11, 4), (2026, 12, 16)),
            ["ECB"] = Utc(12, 15,
                (2025, 1, 30), (2025, 3, 6), (2025, 4, 17), (2025, 6, 5), (2025, 7, 24), (2025, 9, 11),
                (2025, 10, 30), (2025, 12, 18),
                (2026, 1, 29), (2026, 3, 12), (2026, 4, 16), (2026, 6, 4), (2026, 7, 23), (2026, 9, 10),
                (2026, 10, 29), (2026, 12, 17)),
            ["BOE"] = Utc(12, 0,
                (2025, 2, 6), (2025, 3, 20), (2025, 5, 8), (2025, 6, 19), (2025, 8, 7), (2025, 9, 18),
                (2025, 11, 6), (2025, 12, 18),
                (2026, 2, 5), (2026, 3, 19), (2026, 5, 7), (2026, 6, 18), (2026, 8, 6), (2026, 9, 17),
                (2026, 11, 5), (2026, 12, 17))
        };

    public Task<IReadOnlyList<SourceScheduleItem>> FetchScheduleAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        IReadOnlyList<SourceScheduleItem> items = Meetings.TryGetValue(sourceSeriesId, out var dates)
            ? dates.Where(d => d >= from && d <= to)
                .Select(d => new SourceScheduleItem(sourceSeriesId, ReleaseWindow.Exact(d), "UTC"))
                .ToList()
            : [];
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<SourceReleaseItem>> FetchReleasesAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SourceReleaseItem>>([]);

    private static DateTimeOffset[] Utc(int hour, int minute, params (int Year, int Month, int Day)[] days) =>
        days.Select(d => new DateTimeOffset(d.Year, d.Month, d.Day, hour, minute, 0, TimeSpan.Zero)).ToArray();
}
