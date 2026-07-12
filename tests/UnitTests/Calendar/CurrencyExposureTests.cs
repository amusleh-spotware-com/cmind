using Core;
using Core.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CurrencyExposureTests
{
    [Fact]
    public void Fx_pair_exposes_both_legs()
    {
        var ccy = CurrencyExposure.CurrenciesOf(new Symbol("EURUSD")).Select(c => c.Value);
        ccy.Should().BeEquivalentTo(["EUR", "USD"]);
    }

    [Fact]
    public void Metal_pair_exposes_only_the_quote_currency()
    {
        CurrencyExposure.CurrenciesOf(new Symbol("XAUUSD")).Select(c => c.Value).Should().BeEquivalentTo(["USD"]);
    }

    [Fact]
    public void Index_symbol_maps_to_its_currency()
    {
        CurrencyExposure.CurrenciesOf(new Symbol("US500")).Select(c => c.Value).Should().BeEquivalentTo(["USD"]);
        CurrencyExposure.CurrenciesOf(new Symbol("GER40")).Select(c => c.Value).Should().BeEquivalentTo(["EUR"]);
    }

    [Fact]
    public void Euro_area_members_all_fan_in_to_eur()
    {
        CurrencyExposure.CurrenciesOf(new CountryCode("DE")).Select(c => c.Value).Should().BeEquivalentTo(["EUR"]);
        CurrencyExposure.CurrenciesOf(new CountryCode("FR")).Select(c => c.Value).Should().BeEquivalentTo(["EUR"]);
        CurrencyExposure.CurrenciesOf(new CountryCode("EU")).Select(c => c.Value).Should().BeEquivalentTo(["EUR"]);
    }

    [Fact]
    public void EURUSD_is_affected_by_both_EU_and_US_events()
    {
        var watchlist = new[] { new Symbol("EURUSD") };
        CurrencyExposure.AffectedSymbols(new CountryCode("US"), watchlist).Should().ContainSingle();
        CurrencyExposure.AffectedSymbols(new CountryCode("DE"), watchlist).Should().ContainSingle();
    }

    [Fact]
    public void Unrelated_country_does_not_affect_symbol()
    {
        var watchlist = new[] { new Symbol("EURUSD") };
        CurrencyExposure.AffectedSymbols(new CountryCode("JP"), watchlist).Should().BeEmpty();
        CurrencyExposure.Affects(new CountryCode("JP"), new Symbol("EURUSD")).Should().BeFalse();
    }

    [Fact]
    public void AffectedSymbols_filters_a_mixed_watchlist()
    {
        var watchlist = new[]
        {
            new Symbol("EURUSD"), new Symbol("USDJPY"), new Symbol("GBPCHF"), new Symbol("XAUUSD")
        };

        var affected = CurrencyExposure.AffectedSymbols(new CountryCode("US"), watchlist).Select(s => s.Value);
        affected.Should().BeEquivalentTo(["EURUSD", "USDJPY", "XAUUSD"]);
    }
}
