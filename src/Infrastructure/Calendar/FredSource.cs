using System.Globalization;
using System.Text.Json;
using Core.Calendar;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// FRED (St. Louis Fed) connector — the best all-around primary source: full history plus vintage dates for
/// point-in-time revisions. Fetches observations for a series and maps each to a source release item, using
/// the observation's <c>realtime_start</c> as the <c>KnownAt</c> anchor. Absent an API key it degrades to an
/// empty result so the rest of the calendar keeps working. Lives behind a resilient typed <c>HttpClient</c>.
/// </summary>
public sealed class FredSource(HttpClient httpClient, IOptionsMonitor<AppOptions> options) : ICalendarSource
{
    public string Name => "FRED";

    // FRED publishes only realized observations, not a forward schedule; scheduling for FRED series is
    // inferred from the release cadence elsewhere, so this returns nothing (a well-formed empty result).
    public Task<IReadOnlyList<SourceScheduleItem>> FetchScheduleAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SourceScheduleItem>>([]);

    public async Task<IReadOnlyList<SourceReleaseItem>> FetchReleasesAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var calendar = options.CurrentValue.Calendar;
        if (string.IsNullOrWhiteSpace(calendar.FredApiKey)) return [];

        var url = $"series/observations?series_id={Uri.EscapeDataString(sourceSeriesId)}"
                  + $"&api_key={Uri.EscapeDataString(calendar.FredApiKey)}&file_type=json"
                  + $"&observation_start={from:yyyy-MM-dd}&observation_end={to:yyyy-MM-dd}";

        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return Parse(stream);
    }

    /// <summary>Parses a FRED <c>series/observations</c> JSON payload into source release items.</summary>
    public static IReadOnlyList<SourceReleaseItem> Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("observations", out var observations)) return [];

        var items = new List<SourceReleaseItem>();
        foreach (var observation in observations.EnumerateArray())
        {
            if (!observation.TryGetProperty("date", out var dateElement)) continue;
            if (!observation.TryGetProperty("value", out var valueElement)) continue;

            var rawValue = valueElement.GetString();
            if (string.IsNullOrWhiteSpace(rawValue) || rawValue == "."
                || !decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var actual))
                continue;

            if (!TryParseDate(dateElement.GetString(), out var effectiveAt)) continue;

            var knownAt = observation.TryGetProperty("realtime_start", out var rtStart)
                          && TryParseDate(rtStart.GetString(), out var rt)
                ? rt
                : effectiveAt;

            items.Add(new SourceReleaseItem(
                SourceSeriesId: string.Empty,
                EffectiveAt: effectiveAt,
                KnownAt: knownAt,
                Actual: actual,
                Previous: null,
                Unit: null,
                SourceRef: $"fred:{effectiveAt:yyyy-MM-dd}"));
        }

        return items;
    }

    private static bool TryParseDate(string? value, out DateTimeOffset instant)
    {
        instant = default;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        instant = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return true;
    }
}
