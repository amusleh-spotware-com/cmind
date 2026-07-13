using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.PropFirm;

// Invariants for the PropRule aggregate: construction/update range guards, flatten stamping, enable.
// (WS-1 Core backfill.)
public class PropRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 8, 0, 0, TimeSpan.Zero);

    private static PropRule NewRule() =>
        PropRule.Create(UserId.New(), TradingAccountId.New(), "prop", 3, dailyLossLimit: 100, maxDrawdownPercent: 10,
            autoFlatten: true);

    [Fact]
    public void Create_sets_fields_and_is_enabled()
    {
        var rule = NewRule();

        rule.Name.Should().Be("prop");
        rule.MaxConcurrentLiveInstances.Should().Be(3);
        rule.DailyLossLimit.Should().Be(100);
        rule.MaxDrawdownPercent.Should().Be(10);
        rule.AutoFlatten.Should().BeTrue();
        rule.Enabled.Should().BeTrue();
        rule.LastFlattenedAt.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_blank_name_negative_loss_and_out_of_range_concurrency()
    {
        var blank = () => PropRule.Create(UserId.New(), TradingAccountId.New(), " ", 1, 0, 0, false);
        blank.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);

        var negLoss = () => PropRule.Create(UserId.New(), TradingAccountId.New(), "p", 1, -1, 0, false);
        negLoss.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.DrawdownOutOfRange);

        var badConcurrency = () => PropRule.Create(UserId.New(), TradingAccountId.New(), "p", -1, 0, 0, false);
        badConcurrency.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MaxConcurrentOutOfRange);
    }

    [Fact]
    public void Update_mutates_all_fields_under_the_same_guards()
    {
        var rule = NewRule();

        rule.Update("renamed", 5, dailyLossLimit: 250, maxDrawdownPercent: 20, autoFlatten: false, enabled: false);

        rule.Name.Should().Be("renamed");
        rule.MaxConcurrentLiveInstances.Should().Be(5);
        rule.DailyLossLimit.Should().Be(250);
        rule.MaxDrawdownPercent.Should().Be(20);
        rule.AutoFlatten.Should().BeFalse();
        rule.Enabled.Should().BeFalse();

        var negLoss = () => rule.Update("p", 1, -5, 0, false, true);
        negLoss.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.DrawdownOutOfRange);
    }

    [Fact]
    public void Record_flattened_stamps_the_time_and_set_enabled_toggles()
    {
        var rule = NewRule();

        rule.RecordFlattened(Now);
        rule.LastFlattenedAt.Should().Be(Now);

        rule.SetEnabled(false);
        rule.Enabled.Should().BeFalse();
    }
}
