using System.Linq;
using Core.Domain;
using Core.Execution;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class ExecutionSchedulerTests
{
    private readonly AlmgrenChrissScheduler _scheduler = new();

    [Fact]
    public void Slices_sum_to_the_total_quantity()
    {
        var slices = _scheduler.Schedule(100, 5, riskAversion: 2, volatility: 0.02, temporaryImpact: 0.1);
        slices.Should().HaveCount(5);
        slices.Sum(s => s.Quantity).Should().BeApproximately(100, 1e-9);
    }

    [Fact]
    public void Risk_neutral_is_an_even_twap()
    {
        var slices = _scheduler.Schedule(100, 4, riskAversion: 0, volatility: 0.02, temporaryImpact: 0.1);
        slices.Should().OnlyContain(s => Math.Abs(s.Quantity - 25) < 1e-9);
    }

    [Fact]
    public void Risk_aversion_front_loads_the_schedule()
    {
        var slices = _scheduler.Schedule(100, 6, riskAversion: 5, volatility: 0.03, temporaryImpact: 0.05);
        slices.First().Quantity.Should().BeGreaterThan(slices.Last().Quantity);
        slices.Sum(s => s.Quantity).Should().BeApproximately(100, 1e-9);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-10, 5)]
    [InlineData(100, 0)]
    public void Rejects_invalid_inputs(double total, int slices)
    {
        var act = () => _scheduler.Schedule(total, slices, 1, 0.02, 0.1);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.execution.input_invalid");
    }
}
