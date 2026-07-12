using Core.Ai.CurrencyStrength;
using FluentAssertions;
using Infrastructure.Ai.CurrencyStrength;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

public sealed class CurrencyMacroParserTests
{
    private readonly CurrencyUniverse _universe = CurrencyUniverse.Of([Major("USD"), Major("EUR"), Exotic("TRY")]);

    [Fact]
    public void Parses_trajectories_and_gap_fill_from_valid_json()
    {
        const string json = """
        {"currencies":[
          {"code":"USD","trajectory":{"ratePathBp":-50,"inflationTrend":-0.5,"growthMomentum":0.3,"geopoliticalDelta":1.0},"dataConfidence":"High"},
          {"code":"TRY","trajectory":{"ratePathBp":200,"inflationTrend":2.0,"growthMomentum":-0.5,"geopoliticalDelta":-2.0},
           "currentGapFill":{"policyRate":45,"cpi":60,"realYield":-15},"dataConfidence":"Low"}
        ]}
        """;

        var gather = CurrencyMacroParser.Parse(json, _universe);

        gather.Trajectories.Should().HaveCount(2);
        gather.Trajectories.Single(t => t.Currency.Code == "USD").ExpectedRatePathBp.Should().Be(-50);
        gather.GapFill.Should().ContainKey("TRY");
        gather.GapFill["TRY"].PolicyRate.Should().Be(45);
        gather.GapFill["TRY"].Confidence.Should().Be(DataConfidence.Low);
    }

    [Fact]
    public void Strips_code_fences_before_parsing()
    {
        const string json = "```json\n{\"currencies\":[{\"code\":\"USD\",\"trajectory\":{\"ratePathBp\":10}}]}\n```";
        var gather = CurrencyMacroParser.Parse(json, _universe);
        gather.Trajectories.Should().ContainSingle(t => t.Currency.Code == "USD");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("{\"currencies\":\"oops\"}")]
    public void Degrades_to_empty_on_malformed_or_partial(string json)
    {
        CurrencyMacroParser.Parse(json, _universe).HasForward.Should().BeFalse();
    }

    [Fact]
    public void Skips_unknown_codes_and_missing_trajectory()
    {
        const string json = """
        {"currencies":[
          {"code":"ZZZ","trajectory":{"ratePathBp":10}},
          {"code":"EUR","currentGapFill":{"policyRate":3}}
        ]}
        """;

        var gather = CurrencyMacroParser.Parse(json, _universe);

        gather.Trajectories.Should().BeEmpty("ZZZ is outside the universe and EUR has no trajectory");
        gather.GapFill.Should().ContainKey("EUR");
    }

    [Fact]
    public void Skips_a_trajectory_whose_rate_path_is_out_of_range()
    {
        const string json = """{"currencies":[{"code":"USD","trajectory":{"ratePathBp":999999}}]}""";
        CurrencyMacroParser.Parse(json, _universe).Trajectories.Should().BeEmpty();
    }
}
