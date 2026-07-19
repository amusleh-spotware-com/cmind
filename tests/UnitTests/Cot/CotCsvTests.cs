using Core.Cot;
using FluentAssertions;
using Xunit;

namespace UnitTests.Cot;

public class CotCsvTests
{
    [Fact]
    public void Build_emits_header_and_a_row_per_report_with_category_columns()
    {
        var point = new CotHistoryPoint(
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
            OpenInterest: 700000,
            SpeculatorNet: 80000,
            CotIndex: 63.5,
            Categories:
            [
                new CotCategoryView(CotTraderCategory.NonCommercial, 200000, 120000, 30000, 80000, 28.5, 17.1, null, null),
                new CotCategoryView(CotTraderCategory.Commercial, 300000, 350000, 0, -50000, 42.8, 50.0, null, null),
                new CotCategoryView(CotTraderCategory.NonReportable, 40000, 50000, 0, -10000, 5.7, 7.1, null, null)
            ]);

        var csv = CotCsv.Build(CotReportKind.Legacy, [point]);
        var lines = csv.TrimEnd('\n').Split('\n');

        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("report_date,open_interest,cot_index,speculator_net,")
            .And.Contain("NonCommercial_long,NonCommercial_short,NonCommercial_spread,NonCommercial_net")
            .And.Contain("NonReportable_net");
        lines[1].Should().StartWith("2024-01-02,700000,63.5,80000,")
            .And.Contain("200000,120000,30000,80000")
            .And.EndWith("40000,50000,0,-10000");
    }

    [Fact]
    public void Build_leaves_a_blank_cot_index_cell_when_absent()
    {
        var point = new CotHistoryPoint(
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), 100, 0, null, []);

        var csv = CotCsv.Build(CotReportKind.Legacy, [point]);
        var row = csv.TrimEnd('\n').Split('\n')[1];

        row.Should().StartWith("2024-01-02,100,,0,");
    }
}
