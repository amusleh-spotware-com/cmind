using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// The CSV import/export anti-corruption layer for the symbol map: it must round-trip, tolerate an optional
// multiplier column, skip the header/blank lines, and reject malformed input through the domain VOs.
public sealed class CopySymbolMapCsvTests
{
    [Fact]
    public void Parse_reads_rows_skips_header_and_blanks_and_defaults_the_multiplier()
    {
        var csv = "Source,Destination,VolumeMultiplier\nEURUSD,EURUSD.x,2\n\nGBPUSD,GBPUSD\n";

        var entries = CopySymbolMapCsv.Parse(csv);

        entries.Should().HaveCount(2);
        entries[0].Source.Value.Should().Be("EURUSD");
        entries[0].Destination.Value.Should().Be("EURUSD.X", "Symbol normalizes to upper-case");
        entries[0].VolumeMultiplier.Should().Be(2);
        entries[1].VolumeMultiplier.Should().Be(1, "an omitted multiplier defaults to 1");
    }

    [Fact]
    public void Format_then_parse_round_trips()
    {
        var csv = CopySymbolMapCsv.Format([("EURUSD", "EURUSDX", 1.5), ("GBPUSD", "GBPUSD", 1.0)]);

        var entries = CopySymbolMapCsv.Parse(csv);

        entries.Should().HaveCount(2);
        entries[0].Destination.Value.Should().Be("EURUSDX");
        entries[0].VolumeMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void Parse_rejects_a_row_missing_the_destination()
    {
        var act = () => CopySymbolMapCsv.Parse("EURUSD\n");
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopySymbolMapCsvInvalid);
    }

    [Fact]
    public void Parse_rejects_a_non_numeric_multiplier()
    {
        var act = () => CopySymbolMapCsv.Parse("EURUSD,EURUSD,notanumber\n");
        act.Should().Throw<DomainException>().Which.Message.Should().Be(DomainErrors.CopySymbolMapCsvInvalid);
    }
}
