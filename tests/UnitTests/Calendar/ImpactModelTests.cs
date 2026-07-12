using Core.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class ImpactModelTests
{
    [Fact]
    public void Score_is_reproducible_for_the_same_inputs()
    {
        var inputs = new ImpactInputs(0.9, 0.004, 0.7);
        ImpactModel.Score(inputs).Should().Be(ImpactModel.Score(inputs));
    }

    [Fact]
    public void Score_stamps_the_model_version()
    {
        ImpactModel.Score(new ImpactInputs(0.5, 0.001, 0.5)).Version.Should().Be(ImpactModel.Version);
    }

    [Fact]
    public void Score_is_monotonic_in_vol_footprint()
    {
        var low = ImpactModel.Score(new ImpactInputs(0.5, 0.001, 0.5)).Score.Value;
        var high = ImpactModel.Score(new ImpactInputs(0.5, 0.004, 0.5)).Score.Value;
        high.Should().BeGreaterThan(low);
    }

    [Fact]
    public void Score_is_monotonic_in_surprise_sensitivity()
    {
        var low = ImpactModel.Score(new ImpactInputs(0.5, 0.002, 0.2)).Score.Value;
        var high = ImpactModel.Score(new ImpactInputs(0.5, 0.002, 0.9)).Score.Value;
        high.Should().BeGreaterThan(low);
    }

    [Fact]
    public void Score_saturates_and_stays_within_bounds()
    {
        var maxed = ImpactModel.Score(new ImpactInputs(1, 10, 1));
        maxed.Score.Value.Should().Be(100);
        maxed.Level.Should().Be(ImpactLevel.Critical);

        var floored = ImpactModel.Score(new ImpactInputs(0, 0, 0));
        floored.Score.Value.Should().Be(0);
        floored.Level.Should().Be(ImpactLevel.Low);
    }

    [Theory]
    [InlineData(0, ImpactLevel.Low)]
    [InlineData(30, ImpactLevel.Medium)]
    [InlineData(60, ImpactLevel.High)]
    [InlineData(90, ImpactLevel.Critical)]
    public void Bands_classify_by_default_thresholds(double score, ImpactLevel expected)
    {
        ImpactBands.Default.Classify(new ImpactScore(score)).Should().Be(expected);
    }
}
