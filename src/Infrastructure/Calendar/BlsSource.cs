using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Calendar;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// BLS (US Bureau of Labor Statistics) connector — the authority for CPI, PPI, employment and JOLTS. Its
/// public API v2 takes a POST of series ids + a year range and returns monthly observations. Mapped to source
/// release items keyed by the observation's month. Absent the endpoint/data it degrades to empty; lives
/// behind the shared resilient, rate-limited client.
/// </summary>
public sealed class BlsSource(HttpClient httpClient, IOptionsMonitor<AppOptions> options) : ICalendarSource
{
    public string Name => "BLS";

    public Task<IReadOnlyList<SourceScheduleItem>> FetchScheduleAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SourceScheduleItem>>([]);

    public async Task<IReadOnlyList<SourceReleaseItem>> FetchReleasesAsync(
        string sourceSeriesId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var calendar = options.CurrentValue.Calendar;
        var payload = new
        {
            seriesid = new[] { sourceSeriesId },
            startyear = from.Year.ToString(CultureInfo.InvariantCulture),
            endyear = to.Year.ToString(CultureInfo.InvariantCulture),
            registrationkey = calendar.BlsApiKey
        };

        using var response = await httpClient.PostAsJsonAsync(string.Empty, payload, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return Parse(stream);
    }

    /// <summary>Parses a BLS API v2 <c>timeseries/data</c> JSON payload into source release items.</summary>
    public static IReadOnlyList<SourceReleaseItem> Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Results", out var results)
            || !results.TryGetProperty("series", out var series))
            return [];

        var items = new List<SourceReleaseItem>();
        foreach (var entry in series.EnumerateArray())
        {
            if (!entry.TryGetProperty("data", out var data)) continue;
            foreach (var observation in data.EnumerateArray())
            {
                if (!observation.TryGetProperty("period", out var periodElement)) continue;
                var period = periodElement.GetString();
                if (period is null || period.Length != 3 || period[0] != 'M'
                    || !int.TryParse(period.AsSpan(1), out var month) || month is < 1 or > 12)
                    continue; // skip annual (M13) and malformed periods

                if (!observation.TryGetProperty("year", out var yearElement)
                    || !int.TryParse(yearElement.GetString(), out var year))
                    continue;

                if (!observation.TryGetProperty("value", out var valueElement)
                    || !decimal.TryParse(valueElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var actual))
                    continue;

                var effectiveAt = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
                items.Add(new SourceReleaseItem(
                    string.Empty, effectiveAt, effectiveAt, actual, null, null, $"bls:{year}-{month:00}"));
            }
        }

        return items;
    }
}
