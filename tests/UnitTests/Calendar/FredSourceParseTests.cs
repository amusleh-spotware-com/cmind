using System.Text;
using Core.Calendar;
using Infrastructure.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class FredSourceParseTests
{
    private static Stream Json(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Fact]
    public void Parses_observations_into_release_items_with_knownat_from_realtime_start()
    {
        const string payload = """
        {
          "observations": [
            { "realtime_start": "2024-02-13", "date": "2024-01-01", "value": "3.1" },
            { "realtime_start": "2024-03-12", "date": "2024-02-01", "value": "3.2" }
          ]
        }
        """;

        var items = FredSource.Parse(Json(payload));

        items.Should().HaveCount(2);
        items[0].Actual.Should().Be(3.1m);
        items[0].EffectiveAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        items[0].KnownAt.Should().Be(new DateTimeOffset(2024, 2, 13, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Skips_missing_values_marked_with_a_dot()
    {
        const string payload = """
        { "observations": [ { "date": "2024-01-01", "value": "." }, { "date": "2024-02-01", "value": "5" } ] }
        """;

        var items = FredSource.Parse(Json(payload));

        items.Should().ContainSingle();
        items[0].Actual.Should().Be(5m);
    }

    [Fact]
    public void Empty_payload_yields_no_items()
    {
        FredSource.Parse(Json("""{ "observations": [] }""")).Should().BeEmpty();
        FredSource.Parse(Json("""{ }""")).Should().BeEmpty();
    }
}
