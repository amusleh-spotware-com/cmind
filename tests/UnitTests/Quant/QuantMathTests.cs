using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class QuantMathTests
{
    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(1.0, 0.8413447460)]
    [InlineData(-1.0, 0.1586552540)]
    [InlineData(1.959963985, 0.975)]
    [InlineData(-1.959963985, 0.025)]
    [InlineData(2.5758293035, 0.995)]
    public void NormalCdf_matches_known_values(double x, double expected)
    {
        QuantMath.NormalCdf(x).Should().BeApproximately(expected, 1e-6);
    }

    [Theory]
    [InlineData(0.5, 0.0)]
    [InlineData(0.975, 1.959963985)]
    [InlineData(0.025, -1.959963985)]
    [InlineData(0.995, 2.5758293035)]
    public void NormalInverseCdf_matches_known_values(double p, double expected)
    {
        QuantMath.NormalInverseCdf(p).Should().BeApproximately(expected, 1e-6);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.37)]
    [InlineData(0.5)]
    [InlineData(0.83)]
    [InlineData(0.99)]
    public void Cdf_and_inverse_round_trip(double p)
    {
        QuantMath.NormalCdf(QuantMath.NormalInverseCdf(p)).Should().BeApproximately(p, 1e-6);
    }

    [Fact]
    public void NormalInverseCdf_saturates_at_bounds()
    {
        QuantMath.NormalInverseCdf(0.0).Should().Be(double.NegativeInfinity);
        QuantMath.NormalInverseCdf(1.0).Should().Be(double.PositiveInfinity);
    }
}
