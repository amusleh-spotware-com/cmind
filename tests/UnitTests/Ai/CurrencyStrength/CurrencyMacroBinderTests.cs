using Core.Ai.CurrencyStrength;
using FluentAssertions;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

public sealed class CurrencyMacroBinderTests
{
    [Fact]
    public void Calendar_figures_win_over_ai_gap_fill()
    {
        var calendar = new CurrencyMacroInputs { PolicyRate = 5.0, Cpi = 3.0, GdpGrowth = 2.0, Unemployment = 4.0, Confidence = DataConfidence.High };
        var ai = new CurrencyMacroInputs { PolicyRate = 9.9, RealYield = 1.5, Confidence = DataConfidence.Low };

        var merged = CurrencyMacroBinder.Merge(Major("USD"), calendar, ai);

        merged.PolicyRate.Should().Be(5.0, "calendar is the primary source");
        merged.RealYield.Should().Be(1.5, "AI fills only what the calendar lacked");
        merged.Provenance.Should().Be(Provenance.Calendar);
    }

    [Fact]
    public void Ai_only_when_calendar_has_no_core_figure()
    {
        var ai = new CurrencyMacroInputs { PolicyRate = 12.0, Cpi = 20.0, GdpGrowth = 1.0, Unemployment = 9.0, Confidence = DataConfidence.Low };

        var merged = CurrencyMacroBinder.Merge(Em("BRL"), null, ai);

        merged.Provenance.Should().Be(Provenance.Ai);
        merged.PolicyRate.Should().Be(12.0);
        merged.Confidence.Should().Be(DataConfidence.Low);
    }

    [Fact]
    public void Missing_everywhere_falls_to_neutral_defaults()
    {
        var merged = CurrencyMacroBinder.Merge(Major("USD"), null, null);

        merged.PolicyRate.Should().Be(0.0);
        merged.InflationTarget.Should().Be(2.0);
        merged.Cpi.Should().Be(2.0);
        merged.Stance.Should().Be(PolicyStance.Neutral);
    }
}
