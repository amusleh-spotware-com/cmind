using Core.Options;
using FluentAssertions;
using Infrastructure.Calendar;
using IntegrationTests.CopyLive;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Calendar;

/// <summary>
/// Exercises the REAL FRED and BLS connectors end-to-end against the live provider APIs, driving the exact
/// same <see cref="FredSource"/>/<see cref="BlsSource"/> code the ingestion worker uses. The API keys come
/// from the unified dev-credentials file (<c>secrets/dev-credentials.local.json</c> → <c>Calendar.FredApiKey</c>
/// / <c>Calendar.BlsApiKey</c>), with the <c>FRED_API_KEY</c>/<c>BLS_API_KEY</c> environment variables taking
/// precedence. When a key is absent the matching test skips cleanly — CI stays green while the path is still
/// covered whenever a dev (or the cluster's mounted Secret) supplies the key.
/// </summary>
public sealed class CalendarSourceLiveTests(ITestOutputHelper output)
{
    // Fixed historical window that always has published data (no wall-clock — mandate 4).
    private static readonly DateTimeOffset From = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fred_returns_observations_for_a_known_series_when_a_key_is_configured()
    {
        var apiKey = Environment.GetEnvironmentVariable("FRED_API_KEY") ?? LiveCopySecrets.LoadFredApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            output.WriteLine("FRED API key absent (set Calendar.FredApiKey in secrets/dev-credentials.local.json " +
                             "or FRED_API_KEY) — skipping the live FRED test.");
            return;
        }

        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions
        {
            Calendar = new CalendarOptions { FredApiKey = apiKey }
        });
        var baseUrl = options.CurrentValue.Calendar.FredBaseUrl;
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/") };
        var source = new FredSource(http, options);

        // UNRATE = US civilian unemployment rate, monthly, decades of history.
        var releases = await source.FetchReleasesAsync("UNRATE", From, To, CancellationToken.None);

        releases.Should().NotBeEmpty("a live FRED key must return observations for UNRATE over 2022");
    }

    [Fact]
    public async Task Bls_returns_observations_for_a_known_series_when_a_key_is_configured()
    {
        var apiKey = Environment.GetEnvironmentVariable("BLS_API_KEY") ?? LiveCopySecrets.LoadBlsApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            output.WriteLine("BLS API key absent (set Calendar.BlsApiKey in secrets/dev-credentials.local.json " +
                             "or BLS_API_KEY) — skipping the live BLS test.");
            return;
        }

        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions
        {
            Calendar = new CalendarOptions { BlsApiKey = apiKey }
        });
        using var http = new HttpClient { BaseAddress = new Uri(options.CurrentValue.Calendar.BlsBaseUrl) };
        var source = new BlsSource(http, options);

        // CUUR0000SA0 = CPI for All Urban Consumers, all items, monthly.
        var releases = await source.FetchReleasesAsync("CUUR0000SA0", From, To, CancellationToken.None);

        releases.Should().NotBeEmpty("a live BLS key must return CPI observations over 2022");
    }
}
