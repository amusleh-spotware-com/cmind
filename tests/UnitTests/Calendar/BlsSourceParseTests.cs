using System.Text;
using Core.Options;
using Infrastructure.Calendar;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace UnitTests.Calendar;

public sealed class BlsSourceParseTests
{
    private static Stream Json(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("BLS must not hit the network when no API key is configured.");
    }

    [Fact]
    public async Task No_api_key_yields_no_items_without_touching_the_network()
    {
        using var http = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://api.bls.gov/") };
        var options = Substitute.For<Microsoft.Extensions.Options.IOptionsMonitor<AppOptions>>();
        options.CurrentValue.Returns(new AppOptions()); // Calendar.BlsApiKey is null by default

        var source = new BlsSource(http, options);

        var items = await source.FetchReleasesAsync("CUUR0000SA0",
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        items.Should().BeEmpty();
    }

    [Fact]
    public void Parses_monthly_observations_into_release_items()
    {
        const string payload = """
        {
          "status": "REQUEST_SUCCEEDED",
          "Results": {
            "series": [
              {
                "seriesID": "CUUR0000SA0",
                "data": [
                  { "year": "2024", "period": "M02", "periodName": "February", "value": "310.3" },
                  { "year": "2024", "period": "M01", "periodName": "January", "value": "308.4" }
                ]
              }
            ]
          }
        }
        """;

        var items = BlsSource.Parse(Json(payload));

        items.Should().HaveCount(2);
        items[0].Actual.Should().Be(310.3m);
        items[0].EffectiveAt.Should().Be(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Skips_annual_averages_and_malformed_periods()
    {
        const string payload = """
        { "Results": { "series": [ { "data": [
            { "year": "2024", "period": "M13", "value": "9.9" },
            { "year": "2024", "period": "M05", "value": "5" }
        ] } ] } }
        """;

        var items = BlsSource.Parse(Json(payload));

        items.Should().ContainSingle();
        items[0].Actual.Should().Be(5m);
    }

    [Fact]
    public void Empty_or_error_payload_yields_no_items()
    {
        BlsSource.Parse(Json("""{ "status": "REQUEST_NOT_PROCESSED" }""")).Should().BeEmpty();
        BlsSource.Parse(Json("""{ "Results": { } }""")).Should().BeEmpty();
    }
}
