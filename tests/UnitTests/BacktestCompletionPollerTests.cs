using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests;

public sealed class BacktestCompletionPollerTests
{
    private static readonly DateTimeOffset Start = new(2026, 07, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Max = TimeSpan.FromHours(6);

    [Fact]
    public void IsOverdue_true_past_max_duration()
        => BacktestCompletionPoller.IsOverdue(Start, Start + Max + TimeSpan.FromMinutes(1), Max).Should().BeTrue();

    [Fact]
    public void IsOverdue_false_within_max_duration()
        => BacktestCompletionPoller.IsOverdue(Start, Start + Max - TimeSpan.FromMinutes(1), Max).Should().BeFalse();

    [Fact]
    public void IsOverdue_false_exactly_at_max_duration()
        => BacktestCompletionPoller.IsOverdue(Start, Start + Max, Max).Should().BeFalse();

    [Fact]
    public void DescribeMissingReport_flags_ctrader_empty_report_crash_with_an_actionable_reason()
    {
        // cTrader's own report writer throws on an empty backtest result.
        var log = "Progress | Backtesting | 0.00 % |\n{\n    \"Equity\":,\n}\nMessage expected\nSystem.InvalidOperationException: Message expected";

        var reason = BacktestCompletionPoller.DescribeMissingReport(log);

        reason.Should().Contain("no backtest results").And.Contain("date range");
    }

    [Fact]
    public void DescribeMissingReport_falls_back_to_the_generic_reason_for_any_other_exit()
    {
        BacktestCompletionPoller.DescribeMissingReport("docker: image pull failed")
            .Should().Be("Container exited without producing a report");
        BacktestCompletionPoller.DescribeMissingReport(null)
            .Should().Be("Container exited without producing a report");
    }
}
