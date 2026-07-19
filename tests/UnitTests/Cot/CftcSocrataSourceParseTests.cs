using System.Text;
using Core.Cot;
using FluentAssertions;
using Infrastructure.Cot;
using Xunit;

namespace UnitTests.Cot;

public class CftcSocrataSourceParseTests
{
    private static Stream Json(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Parses_legacy_row_into_three_categories()
    {
        const string json = """
        [{
          "cftc_contract_market_code":"099741",
          "contract_market_name":"EURO FX",
          "market_and_exchange_names":"EURO FX - CHICAGO MERCANTILE EXCHANGE",
          "report_date_as_yyyy_mm_dd":"2024-01-02T00:00:00.000",
          "open_interest_all":"700000",
          "change_in_open_interest_all":"-1500",
          "noncomm_positions_long_all":"200000","noncomm_positions_short_all":"120000","noncomm_postions_spread_all":"30000",
          "comm_positions_long_all":"300000","comm_positions_short_all":"350000",
          "nonrept_positions_long_all":"40000","nonrept_positions_short_all":"50000"
        }]
        """;

        var reports = CftcSocrataSource.Parse(Json(json), CotReportKind.Legacy, combined: false);

        reports.Should().HaveCount(1);
        var report = reports[0];
        report.ContractCode.Should().Be("099741");
        report.MarketName.Should().Be("EURO FX");
        report.Exchange.Should().Be("CHICAGO MERCANTILE EXCHANGE");
        report.OpenInterest.Should().Be(700000);
        report.OpenInterestChange.Should().Be(-1500);
        report.Categories.Should().HaveCount(3);

        var nonComm = report.Categories.Single(c => c.Category == CotTraderCategory.NonCommercial);
        nonComm.Long.Should().Be(200000);
        nonComm.Short.Should().Be(120000);
        nonComm.Spread.Should().Be(30000);
        report.Categories.Single(c => c.Category == CotTraderCategory.Commercial).Spread.Should().Be(0);
    }

    [Fact]
    public void Parses_disaggregated_swap_double_underscore_columns()
    {
        const string json = """
        [{
          "cftc_contract_market_code":"067651",
          "contract_market_name":"WTI-PHYSICAL",
          "market_and_exchange_names":"WTI-PHYSICAL - NEW YORK MERCANTILE EXCHANGE",
          "report_date_as_yyyy_mm_dd":"2024-01-02T00:00:00.000",
          "open_interest_all":"2000000",
          "prod_merc_positions_long":"500000","prod_merc_positions_short":"600000",
          "swap_positions_long_all":"300000","swap__positions_short_all":"120000","swap__positions_spread_all":"40000",
          "m_money_positions_long_all":"250000","m_money_positions_short_all":"100000","m_money_positions_spread":"20000",
          "other_rept_positions_long":"80000","other_rept_positions_short":"70000","other_rept_positions_spread":"10000",
          "nonrept_positions_long_all":"60000","nonrept_positions_short_all":"50000"
        }]
        """;

        var reports = CftcSocrataSource.Parse(Json(json), CotReportKind.Disaggregated, combined: false);

        var report = reports.Should().ContainSingle().Subject;
        report.Categories.Should().HaveCount(5);
        var swap = report.Categories.Single(c => c.Category == CotTraderCategory.SwapDealer);
        swap.Long.Should().Be(300000);
        swap.Short.Should().Be(120000);
        swap.Spread.Should().Be(40000);
    }

    [Fact]
    public void Parses_tff_dealer_and_leveraged_funds()
    {
        const string json = """
        [{
          "cftc_contract_market_code":"020601",
          "contract_market_name":"UST BOND",
          "market_and_exchange_names":"UST BOND - CHICAGO BOARD OF TRADE",
          "report_date_as_yyyy_mm_dd":"2024-01-02T00:00:00.000",
          "open_interest_all":"1200000",
          "dealer_positions_long_all":"20000","dealer_positions_short_all":"150000","dealer_positions_spread_all":"1000",
          "asset_mgr_positions_long":"590000","asset_mgr_positions_short":"250000","asset_mgr_positions_spread":"200000",
          "lev_money_positions_long":"110000","lev_money_positions_short":"330000","lev_money_positions_spread":"39000",
          "other_rept_positions_long":"80000","other_rept_positions_short":"98000","other_rept_positions_spread":"0",
          "nonrept_positions_long_all":"170000","nonrept_positions_short_all":"143000"
        }]
        """;

        var reports = CftcSocrataSource.Parse(Json(json), CotReportKind.Tff, combined: true);

        var report = reports.Should().ContainSingle().Subject;
        report.Combined.Should().BeTrue();
        var lev = report.Categories.Single(c => c.Category == CotTraderCategory.LeveragedFunds);
        (lev.Long - lev.Short).Should().Be(110000 - 330000);
        report.Categories.Single(c => c.Category == CotTraderCategory.Dealer).Short.Should().Be(150000);
    }

    [Fact]
    public void Skips_rows_without_a_contract_code()
    {
        const string json = """[{"report_date_as_yyyy_mm_dd":"2024-01-02T00:00:00.000","open_interest_all":"1"}]""";
        CftcSocrataSource.Parse(Json(json), CotReportKind.Legacy, combined: false).Should().BeEmpty();
    }

    [Fact]
    public void Dataset_ids_cover_all_six_variants()
    {
        var ids = new[]
        {
            CftcSocrataSource.DatasetId(CotReportKind.Legacy, false),
            CftcSocrataSource.DatasetId(CotReportKind.Legacy, true),
            CftcSocrataSource.DatasetId(CotReportKind.Disaggregated, false),
            CftcSocrataSource.DatasetId(CotReportKind.Disaggregated, true),
            CftcSocrataSource.DatasetId(CotReportKind.Tff, false),
            CftcSocrataSource.DatasetId(CotReportKind.Tff, true)
        };
        ids.Should().OnlyHaveUniqueItems().And.NotContainNulls();
    }
}
