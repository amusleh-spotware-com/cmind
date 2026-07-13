using Core.Ai.CurrencyStrength;
using FluentAssertions;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

// The pure Core mapping from the deterministic engine VOs to the serializable read-model rows.
// (WS-1 Core backfill.)
public sealed class CurrencyStrengthMapperTests
{
    [Fact]
    public void To_rank_rows_projects_every_score_with_its_rank_and_drivers()
    {
        var usd = Major("USD");
        var eur = Major("EUR");
        var ranking = CurrencyStrengthCalculator.Compute(
            [Indicators(usd, policyRate: 5.0), Indicators(eur, policyRate: 3.0)],
            StrengthWeights.Default(), AsOf);

        var rows = CurrencyStrengthMapper.ToRankRows(ranking);

        rows.Should().HaveCount(2);
        rows.Select(r => r.Code).Should().Contain(["USD", "EUR"]);
        var usdRow = rows.Single(r => r.Code == "USD");
        usdRow.Rank.Should().Be(ranking.Rank(usd));
        usdRow.Drivers.Should().NotBeEmpty("each row carries its per-driver contribution breakdown");
    }

    [Fact]
    public void To_layer_projects_forecasts_and_the_pair_matrix()
    {
        var eur = Major("EUR");
        var usd = Major("USD");
        var current = CurrencyStrengthCalculator.Compute(
            [Indicators(eur), Indicators(usd)], StrengthWeights.Default(), AsOf);
        var trajectories = new[] { Trajectory(eur, ratePathBp: 100), Trajectory(usd, ratePathBp: -100) };

        var (forecasts, matrix) = ForwardOutlookCalculator.Project(
            current, trajectories, ForwardWeights.Default(), Horizon.ThreeMonths, AsOf);

        var layer = CurrencyStrengthMapper.ToLayer(forecasts, matrix);

        layer.Forecasts.Should().HaveCount(2);
        layer.Forecasts.Select(f => f.Code).Should().Contain(["EUR", "USD"]);
        layer.Pairs.Should().NotBeEmpty("the forward pair-outlook matrix is projected to rows");
        layer.Pairs.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Base) && !string.IsNullOrEmpty(p.Quote));
    }
}
