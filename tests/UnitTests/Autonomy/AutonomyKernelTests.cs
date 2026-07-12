using System;
using System.Collections.Generic;
using Core.Autonomy;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class AutonomyKernelTests
{
    private static RiskEnvelope Envelope(IReadOnlySet<string>? symbols = null) =>
        new(maxDailyLossPercent: 4, maxOpenExposureLots: 10, maxPositionSizeLots: 2,
            maxLeverage: 30, maxConsecutiveLosses: 3, maxOrdersPerHour: 20, allowedSymbols: symbols);

    [Fact]
    public void Envelope_rejects_incoherent_limits()
    {
        var posAboveOpen = () => new RiskEnvelope(4, 5, 10, 30, 3, 20);
        posAboveOpen.Should().Throw<DomainException>().Which.Code.Should().Be("domain.autonomy.risk_envelope_invalid");

        var badLoss = () => new RiskEnvelope(0, 10, 2, 30, 3, 20);
        badLoss.Should().Throw<DomainException>().Which.Code.Should().Be("domain.autonomy.risk_envelope_invalid");
    }

    [Fact]
    public void Envelope_allows_a_compliant_order()
    {
        Envelope().CheckOrder("EURUSD", sizeLots: 1, openExposureLots: 2, ordersThisHour: 1).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Envelope_denies_each_breach()
    {
        var e = Envelope(new HashSet<string> { "EURUSD" });
        e.CheckOrder("GBPUSD", 1, 0, 0).Allowed.Should().BeFalse();          // symbol not allowed
        e.CheckOrder("EURUSD", 3, 0, 0).Allowed.Should().BeFalse();          // size > max position
        e.CheckOrder("EURUSD", 2, 9, 0).Allowed.Should().BeFalse();          // open exposure cap
        e.CheckOrder("EURUSD", 1, 0, 20).Allowed.Should().BeFalse();         // order rate cap
    }

    [Fact]
    public void Circuit_breaker_trips_on_every_hazard()
    {
        var e = Envelope();
        CircuitBreaker.Evaluate(e, new BreakerMetrics(0, 0, AiAvailable: false, false)).Tripped.Should().BeTrue();
        CircuitBreaker.Evaluate(e, new BreakerMetrics(0, 0, true, HardGoalBreached: true)).Tripped.Should().BeTrue();
        CircuitBreaker.Evaluate(e, new BreakerMetrics(3, 0, true, false)).Tripped.Should().BeTrue();
        CircuitBreaker.Evaluate(e, new BreakerMetrics(0, 4, true, false)).Tripped.Should().BeTrue();
        CircuitBreaker.Evaluate(e, new BreakerMetrics(1, 1, true, false)).Tripped.Should().BeFalse();
    }

    [Fact]
    public void Consent_requires_a_version_and_stamp_and_tracks_currency()
    {
        var bad = () => new DisclaimerConsent(0, DateTimeOffset.UnixEpoch);
        bad.Should().Throw<DomainException>().Which.Code.Should().Be("domain.autonomy.disclaimer_consent_invalid");

        var consent = new DisclaimerConsent(2, DateTimeOffset.UnixEpoch);
        consent.IsCurrent(2).Should().BeTrue();
        consent.IsCurrent(3).Should().BeFalse();
    }

    [Theory]
    [InlineData(3.0, GoalStatus.OnTrack)]
    [InlineData(3.8, GoalStatus.AtRisk)]
    [InlineData(5.0, GoalStatus.Breached)]
    public void Hard_drawdown_target_evaluates(double actual, GoalStatus expected)
    {
        new PerformanceTarget(TargetMetric.MaxDrawdown, TargetComparator.Below, 4.0, TargetEnforcement.Hard)
            .Evaluate(actual).Should().Be(expected);
    }

    [Fact]
    public void Profit_factor_target_and_validation()
    {
        var pf = new PerformanceTarget(TargetMetric.ProfitFactor, TargetComparator.AtLeast, 1.5, TargetEnforcement.Soft);
        pf.Evaluate(2.0).Should().Be(GoalStatus.OnTrack);
        pf.Evaluate(1.0).Should().Be(GoalStatus.Breached);
        pf.IsHard.Should().BeFalse();

        var bad = () => new PerformanceTarget(TargetMetric.MaxDrawdown, TargetComparator.Below, 150, TargetEnforcement.Hard);
        bad.Should().Throw<DomainException>().Which.Code.Should().Be("domain.autonomy.performance_target_invalid");
    }
}
