using System.Collections.Generic;
using System.Linq;
using Core.Journal;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class JournalAnalyzerTests
{
    private readonly JournalAnalyzer _analyzer = new();

    private static JournalEntry Backtest(string symbol, double netProfit) =>
        new(symbol, "Backtest", TradeOutcome.Completed, netProfit);

    [Fact]
    public void Empty_history_reports_no_data()
    {
        var s = _analyzer.Analyze([]);
        s.Total.Should().Be(0);
        s.Insights.Should().ContainSingle().Which.Should().Contain("Not enough");
    }

    [Fact]
    public void Flags_over_concentration()
    {
        var entries = new List<JournalEntry>
        {
            Backtest("EURUSD", 10), Backtest("EURUSD", 5), Backtest("EURUSD", -2), Backtest("EURUSD", 3), Backtest("GBPUSD", 1)
        };
        var s = _analyzer.Analyze(entries);
        s.Insights.Should().Contain(i => i.Contains("Over-concentrated") && i.Contains("EURUSD"));
    }

    [Fact]
    public void Flags_repeated_failures()
    {
        var entries = new List<JournalEntry>
        {
            new("EURUSD", "Run", TradeOutcome.Failed, null),
            new("GBPUSD", "Run", TradeOutcome.Failed, null),
            new("USDJPY", "Run", TradeOutcome.Running, null)
        };
        _analyzer.Analyze(entries).Insights.Should().Contain(i => i.Contains("failed"));
    }

    [Fact]
    public void Flags_losing_bias_and_computes_win_rate()
    {
        var entries = new List<JournalEntry>
        {
            Backtest("EURUSD", 5), Backtest("GBPUSD", -3), Backtest("USDJPY", -4), Backtest("AUDUSD", -1)
        };
        var s = _analyzer.Analyze(entries);
        s.Wins.Should().Be(1);
        s.Losses.Should().Be(3);
        s.WinRate.Should().BeApproximately(0.25, 1e-9);
        s.Insights.Should().Contain(i => i.Contains("losing"));
    }

    [Fact]
    public void Balanced_activity_has_no_leak()
    {
        var entries = new List<JournalEntry>
        {
            Backtest("EURUSD", 5), Backtest("GBPUSD", 3), Backtest("USDJPY", 4), Backtest("AUDUSD", -1)
        };
        _analyzer.Analyze(entries).Insights.Should().Contain(i => i.Contains("Balanced"));
    }
}
