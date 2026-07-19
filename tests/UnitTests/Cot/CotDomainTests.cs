using Core;
using Core.Constants;
using Core.Cot;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Cot;

public class CotDomainTests
{
    private static readonly DateTimeOffset ReportDate = new(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset KnownAt = new(2024, 1, 5, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ContractMarketCode_rejects_blank()
    {
        var act = () => new ContractMarketCode("  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CotContractCodeRequired);
    }

    [Fact]
    public void CotPositions_rejects_negative()
    {
        var act = () => new CotPositions(10, -1, 0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CotPositionNegative);
    }

    [Fact]
    public void CotPositions_net_and_percentages_are_computed()
    {
        var positions = new CotPositions(120, 80, 10);
        positions.Net.Should().Be(40);
        positions.LongPercentOf(400).Should().BeApproximately(30d, 0.001);
        positions.ShortPercentOf(0).Should().Be(0);
    }

    [Fact]
    public void ReportKind_reports_only_its_own_categories()
    {
        CotReportKind.Legacy.Reports(CotTraderCategory.NonCommercial).Should().BeTrue();
        CotReportKind.Legacy.Reports(CotTraderCategory.Dealer).Should().BeFalse();
        CotReportKind.Tff.SpeculatorCategory().Should().Be(CotTraderCategory.LeveragedFunds);
        CotReportKind.Disaggregated.SpeculatorCategory().Should().Be(CotTraderCategory.ManagedMoney);
    }

    [Fact]
    public void Report_create_rejects_negative_open_interest()
    {
        var act = () => NewReport(openInterest: -1);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CotOpenInterestNegative);
    }

    [Fact]
    public void Report_add_position_rejects_category_not_in_kind()
    {
        var report = NewReport();
        var act = () => report.AddPosition(CotTraderCategory.Dealer, new CotPositions(1, 1, 0));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CotCategoryNotInReportKind);
    }

    [Fact]
    public void Report_add_position_rejects_duplicate_category()
    {
        var report = NewReport();
        report.AddPosition(CotTraderCategory.NonCommercial, new CotPositions(10, 5, 2));
        var act = () => report.AddPosition(CotTraderCategory.NonCommercial, new CotPositions(1, 1, 0));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CotCategoryDuplicate);
    }

    [Fact]
    public void Report_speculator_net_reads_the_headline_category()
    {
        var report = NewReport();
        report.AddPosition(CotTraderCategory.NonCommercial, new CotPositions(100, 40, 5));
        report.AddPosition(CotTraderCategory.Commercial, new CotPositions(50, 90, 0));
        report.SpeculatorNet.Should().Be(60);
        report.PositionFor(CotTraderCategory.Commercial)!.Net.Should().Be(-40);
    }

    [Theory]
    [InlineData(new long[] { 10, 20, 30 }, 100d, CotExtreme.LongExtreme)]
    [InlineData(new long[] { 30, 20, 10 }, 0d, CotExtreme.ShortExtreme)]
    [InlineData(new long[] { 10, 10 }, 50d, CotExtreme.None)]
    public void CotIndex_measures_current_against_window(long[] nets, double expected, CotExtreme extreme)
    {
        var index = CotIndexCalculator.Index(nets);
        index.Should().BeApproximately(expected, 0.001);
        CotIndexCalculator.Classify(index).Should().Be(extreme);
    }

    [Fact]
    public void CotIndex_requires_at_least_two_points()
    {
        var act = () => CotIndexCalculator.Index([5]);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CotHistoryInsufficient);
    }

    private static CotReport NewReport(long openInterest = 1000) => CotReport.Create(
        CotMarketId.New(), new ContractMarketCode("099741"), "Euro FX",
        CotReportKind.Legacy, combined: false, ReportDate, KnownAt, openInterest, openInterestChange: 5);
}
