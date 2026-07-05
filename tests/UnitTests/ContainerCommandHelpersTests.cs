using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests;

public class ContainerCommandHelpersTests
{
    [Fact]
    public void ParseEquityCurve_returns_empty_for_null_or_blank()
    {
        ContainerCommandHelpers.ParseEquityCurve(null).Should().BeEmpty();
        ContainerCommandHelpers.ParseEquityCurve("   ").Should().BeEmpty();
    }

    [Fact]
    public void ParseEquityCurve_returns_empty_for_invalid_json()
    {
        ContainerCommandHelpers.ParseEquityCurve("not json").Should().BeEmpty();
    }

    [Fact]
    public void ParseEquityCurve_parses_equityHistory_with_iso_timestamps()
    {
        const string json = """
        {
            "equityHistory": [
                { "time": "2026-01-01T00:00:00Z", "equity": 1000.5 },
                { "time": "2026-01-02T00:00:00Z", "equity": 1050.25 }
            ]
        }
        """;

        var points = ContainerCommandHelpers.ParseEquityCurve(json);

        points.Should().HaveCount(2);
        points[0].Value.Should().Be(1000.5);
        points[1].Value.Should().Be(1050.25);
    }

    [Fact]
    public void ParseEquityCurve_parses_history_with_epoch_millis_and_balance()
    {
        const string json = """
        {
            "history": [
                { "timestamp": 1735689600000, "balance": 500 }
            ]
        }
        """;

        var points = ContainerCommandHelpers.ParseEquityCurve(json);

        points.Should().ContainSingle();
        points[0].Value.Should().Be(500);
        points[0].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1735689600000));
    }

    [Fact]
    public void ParseEquityCurve_ignores_entries_missing_required_fields()
    {
        const string json = """
        {
            "equity": [
                { "time": "2026-01-01T00:00:00Z" },
                { "equity": 100 },
                { "time": "2026-01-01T00:00:00Z", "equity": 100 }
            ]
        }
        """;

        var points = ContainerCommandHelpers.ParseEquityCurve(json);

        points.Should().ContainSingle();
    }

    [Fact]
    public void ParseEquityCurve_returns_empty_when_no_known_array_key_present()
    {
        const string json = """{ "summary": { "netProfit": 100 } }""";

        ContainerCommandHelpers.ParseEquityCurve(json).Should().BeEmpty();
    }
}
