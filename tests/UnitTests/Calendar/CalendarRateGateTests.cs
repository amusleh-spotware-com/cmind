using FluentAssertions;
using Infrastructure.Calendar;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CalendarRateGateTests
{
    private static readonly DateTimeOffset Now = new(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_reservation_sends_immediately_then_subsequent_ones_are_spaced()
    {
        var gate = new CalendarRateGate(new FakeTimeProvider(Now));

        // 60 req/min -> one every second.
        gate.Reserve(60).Should().Be(TimeSpan.Zero);
        gate.Reserve(60).Should().Be(TimeSpan.FromSeconds(1));
        gate.Reserve(60).Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Reservations_advance_as_the_clock_advances()
    {
        var time = new FakeTimeProvider(Now);
        var gate = new CalendarRateGate(time);

        gate.Reserve(60).Should().Be(TimeSpan.Zero);
        time.Advance(TimeSpan.FromSeconds(5));
        // The next allowed slot is already in the past, so no wait.
        gate.Reserve(60).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Penalize_backs_the_gate_off_by_the_retry_after()
    {
        var gate = new CalendarRateGate(new FakeTimeProvider(Now));

        gate.Reserve(600); // tiny interval
        gate.Penalize(TimeSpan.FromSeconds(45));

        gate.Reserve(600).Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Zero_or_negative_rpm_is_clamped_to_one_per_minute()
    {
        var gate = new CalendarRateGate(new FakeTimeProvider(Now));

        gate.Reserve(0).Should().Be(TimeSpan.Zero);
        gate.Reserve(0).Should().Be(TimeSpan.FromMinutes(1));
    }
}
