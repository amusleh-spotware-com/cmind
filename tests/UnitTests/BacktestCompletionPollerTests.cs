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
}
