using Core.Domain;
using FluentAssertions;
using Web.Json;
using Xunit;

namespace IntegrationTests;

// Pure test of the copy-profile import/export helpers (lives here because it references the Web assembly;
// no DB). Covers the settings JSON round-trip, the symbol-map CSV round-trip, and the lenient parse's
// failure branches (bad JSON, header row, malformed/blank lines).
public class CopyProfilePortabilityTests
{
    [Fact]
    public void Settings_json_round_trips_every_field()
    {
        var settings = new CopyProfileSettingsModel
        {
            Mode = MoneyManagementMode.FixedRiskPercent,
            Parameter = 2.5,
            SlippagePips = 1.5,
            Direction = CopyDirectionFilter.LongOnly,
            MinLot = 0.01,
            MaxLot = 5,
            MaxDelaySeconds = 30,
            MaxDrawdownPercent = 20,
            DailyLossLimit = 500,
            Reverse = true,
            ForceMinLot = true,
            CopyStopLoss = false,
            CopyTakeProfit = false,
            MirrorPartialClose = false,
            MirrorScaleIn = true,
            CopyPendingOrders = true,
            CopyTrailingStop = true,
            CopyPendingExpiry = false,
            CopyMasterSlippage = false,
            OrderTypes = CopyOrderTypes.Limit | CopyOrderTypes.Stop,
            SymbolFilterMode = SymbolFilterMode.Whitelist,
            SymbolFilters = ["EURUSD", "GBPUSD"],
            SymbolMap = [new SymbolMapRow("EURUSD", "EURUSD.x", 2)],
        };

        var json = CopyProfilePortability.ExportSettingsJson(settings);
        var back = CopyProfilePortability.ImportSettingsJson(json);

        back.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void Import_settings_returns_null_on_garbage()
    {
        CopyProfilePortability.ImportSettingsJson("not json").Should().BeNull();
        CopyProfilePortability.ImportSettingsJson("").Should().BeNull();
    }

    [Fact]
    public void Symbol_map_csv_round_trips_and_reimports_its_own_header()
    {
        List<SymbolMapRow> rows = [new("EURUSD", "EUR.USD", 1.5), new("GBPUSD", "GBP.USD", 1)];

        var csv = CopyProfilePortability.SymbolMapToCsv(rows);
        csv.Should().StartWith("Source,Destination,VolumeMultiplier");

        var parsed = CopyProfilePortability.ParseSymbolMapCsv(csv);

        parsed.Should().HaveCount(2);
        parsed[0].Should().Be(new SymbolMapRow("EURUSD", "EUR.USD", 1.5));
        parsed[1].Should().Be(new SymbolMapRow("GBPUSD", "GBP.USD", 1));
    }

    [Fact]
    public void Parse_csv_skips_blank_and_malformed_lines_and_defaults_multiplier()
    {
        const string csv = "Source,Destination,VolumeMultiplier\n\nEURUSD,EUR.USD\nonecolumn\nGBPUSD,GBP.USD,abc\n";

        var parsed = CopyProfilePortability.ParseSymbolMapCsv(csv);

        parsed.Should().HaveCount(2);
        parsed[0].Should().Be(new SymbolMapRow("EURUSD", "EUR.USD", 1));
        // Non-numeric multiplier falls back to 1 rather than throwing.
        parsed[1].Should().Be(new SymbolMapRow("GBPUSD", "GBP.USD", 1));
    }

    [Fact]
    public void Parse_csv_returns_empty_for_blank_input()
        => CopyProfilePortability.ParseSymbolMapCsv("   ").Should().BeEmpty();
}
