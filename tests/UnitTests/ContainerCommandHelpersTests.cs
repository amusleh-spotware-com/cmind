using Core;
using Core.Constants;
using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests;

public class ContainerCommandHelpersTests
{
    [Fact]
    public void DataScopeFor_uses_default_when_instance_has_no_trading_account()
    {
        var i = new StartingBacktestInstance { Symbol = "EURUSD", Timeframe = "h1" };

        ContainerCommandHelpers.DataScopeFor(i).Should().Be(FilePaths.DefaultDataScope,
            "a market-data cache dir keyed on the account falls back to a default when there is no account");
    }

    [Theory]
    [InlineData("500123", "500123")]         // a real account number is kept verbatim
    [InlineData("", FilePaths.DefaultDataScope)]
    [InlineData("   ", FilePaths.DefaultDataScope)]
    [InlineData(null, FilePaths.DefaultDataScope)]
    [InlineData("../../etc/passwd", "etcpasswd")] // path separators & dots stripped — no traversal
    [InlineData("a b/c", "abc")]
    public void SanitizeDataScope_constrains_to_a_single_safe_path_segment(string? input, string expected)
    {
        var scope = FilePaths.SanitizeDataScope(input);

        scope.Should().Be(expected);
        scope.Should().NotContain("/").And.NotContain("\\").And.NotContain("..");
    }

    [Fact]
    public void BuildConsoleArgs_backtest_emits_default_data_mode_and_report_flags()
    {
        var i = new StartingBacktestInstance
        {
            Symbol = "EURUSD", Timeframe = "h1",
            BacktestSettingsJson = """{"from":"2024-01-01","to":"2024-02-01"}"""
        };

        var args = ContainerCommandHelpers.BuildConsoleArgs(i, "", false);

        args.Should().StartWith("backtest /mnt/work/cbot.algo");
        args.Should().Contain("--data-dir /mnt/data");
        args.Should().Contain("--data-mode m1");
        args.Should().Contain("--report-json /mnt/work/report.json");
        args.Should().Contain("--report /mnt/work/report.html");
        args.Should().Contain("--exit-on-stop");
    }

    [Fact]
    public void BuildConsoleArgsList_backtest_emits_a_default_non_zero_balance_when_unset()
    {
        var i = new StartingBacktestInstance
        {
            Symbol = "EURUSD", Timeframe = "h1",
            BacktestSettingsJson = """{"from":"2024-01-01","to":"2024-02-01"}"""
        };

        var args = ContainerCommandHelpers.BuildConsoleArgsList(i, "", false);

        // A 0 balance makes cTrader's report saver crash ("Message expected") — every backtest gets a balance.
        args.Should().ContainInConsecutiveOrder("--balance", "10000");
    }

    [Fact]
    public void BuildConsoleArgs_backtest_honors_explicit_balance_and_forwards_extra_settings()
    {
        var i = new StartingBacktestInstance
        {
            Symbol = "EURUSD", Timeframe = "h1",
            BacktestSettingsJson = """{"from":"2024-01-01","to":"2024-02-01","balance":"50000","commission":"3","spread":"1"}"""
        };

        var args = ContainerCommandHelpers.BuildConsoleArgs(i, "", false);

        args.Should().Contain("--balance 50000");
        args.Should().NotContain("--balance 10000");
        args.Should().Contain("--commission 3");
        args.Should().Contain("--spread 1");
    }

    [Fact]
    public void BuildConsoleArgs_backtest_reformats_from_to_dates()
    {
        var i = new StartingBacktestInstance
        {
            Symbol = "EURUSD", Timeframe = "h1",
            BacktestSettingsJson = """{"from":"2024-01-01","to":"2024-02-05"}"""
        };

        var args = ContainerCommandHelpers.BuildConsoleArgs(i, "", false);

        args.Should().Contain("--start \"01/01/2024 00:00\"");
        args.Should().Contain("--end \"05/02/2024 00:00\"");
    }

    [Fact]
    public void BuildConsoleArgs_backtest_honors_data_mode_override()
    {
        var i = new StartingBacktestInstance
        {
            Symbol = "EURUSD", Timeframe = "h1",
            BacktestSettingsJson = """{"from":"2024-01-01","to":"2024-02-01","dataMode":"tick"}"""
        };

        var args = ContainerCommandHelpers.BuildConsoleArgs(i, "", false);

        args.Should().Contain("--data-mode tick");
        args.Should().NotContain("--dataMode");
    }

    [Fact]
    public void BuildConsoleArgs_passes_params_positional_when_present()
    {
        var i = new StartingBacktestInstance { Symbol = "EURUSD", Timeframe = "h1" };

        var withParams = ContainerCommandHelpers.BuildConsoleArgs(i, "", true);
        var withoutParams = ContainerCommandHelpers.BuildConsoleArgs(i, "", false);

        withParams.Should().Contain("/mnt/work/cbot.algo /mnt/work/params.cbotset");
        withoutParams.Should().NotContain("params.cbotset");
    }

    [Fact]
    public void BuildConsoleArgs_run_has_no_backtest_flags()
    {
        var i = new StartingRunInstance { Symbol = "EURUSD", Timeframe = "h1" };

        var args = ContainerCommandHelpers.BuildConsoleArgs(i, "", false);

        args.Should().StartWith("run /mnt/work/cbot.algo");
        args.Should().NotContain("--data-mode");
        args.Should().NotContain("--data-dir");
        args.Should().NotContain("--report-json");
        args.Should().NotContain("--exit-on-stop");
    }

    [Fact]
    public void JsonToCbotset_wraps_params_in_cbotset_json_with_string_values()
    {
        var result = ContainerCommandHelpers.JsonToCbotset("""{"Message":"hi","Periods":14}""");

        result.Should().Be("""{"Parameters":{"Message":"hi","Periods":"14"}}""");
    }

    [Fact]
    public void JsonToCbotset_returns_empty_for_empty_or_invalid()
    {
        ContainerCommandHelpers.JsonToCbotset("{}").Should().BeEmpty();
        ContainerCommandHelpers.JsonToCbotset("not json").Should().BeEmpty();
    }

    [Fact]
    public void BuildConsoleArgsList_tokenizes_backtest_with_unquoted_date_token()
    {
        var i = new StartingBacktestInstance
        {
            Symbol = "EURUSD", Timeframe = "h1",
            BacktestSettingsJson = """{"from":"2024-01-01","to":"2024-02-05"}"""
        };

        var args = ContainerCommandHelpers.BuildConsoleArgsList(i, "", false);

        args[0].Should().Be("backtest");
        args.Should().ContainInConsecutiveOrder("--start", "01/01/2024 00:00");
        args.Should().ContainInConsecutiveOrder("--end", "05/02/2024 00:00");
        args.Should().ContainInConsecutiveOrder("--data-mode", "m1");
        args.Should().Contain("--exit-on-stop");
    }

    [Fact]
    public void BuildConsoleArgsList_run_includes_ctid_and_params_positional_no_datadir()
    {
        var i = new StartingRunInstance { Symbol = "EURUSD", Timeframe = "h1" };

        var args = ContainerCommandHelpers.BuildConsoleArgsList(i, "amusleh", true);

        args.Should().ContainInConsecutiveOrder("run", "/mnt/work/cbot.algo", "/mnt/work/params.cbotset");
        args.Should().ContainInConsecutiveOrder("--ctid", "amusleh");
        args.Should().NotContain("--data-dir");
        args.Should().NotContain("--exit-on-stop");
    }

    [Fact]
    public void ParseEquityCurve_parses_nested_equity_points()
    {
        const string json = """
        {
            "equity": {
                "points": [
                    { "timestamp": 1704067200000, "balance": 1000000, "minEquity": 1, "maxEquity": 2 }
                ]
            }
        }
        """;

        var points = ContainerCommandHelpers.ParseEquityCurve(json);

        points.Should().ContainSingle();
        points[0].Value.Should().Be(1000000);
        points[0].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1704067200000));
    }

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
