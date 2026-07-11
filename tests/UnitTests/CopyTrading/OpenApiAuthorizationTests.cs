using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OpenApiAuthorizationTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 07, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan CriticalWindow = TimeSpan.FromHours(6);

    private static OpenApiAuthorization Create() => OpenApiAuthorization.Create(
        UserId.New(), OpenApiApplicationId.New(), new CtidUserId(4242), isLive: true,
        [1], [2], Expiry, OpenApiScope.Trade);

    [Fact]
    public void MarkRefreshFailed_increments_failure_count_and_does_not_escalate_when_far_from_expiry()
    {
        var auth = Create();
        var now = Expiry - TimeSpan.FromDays(2);

        var escalated = auth.MarkRefreshFailed("boom", now, CriticalWindow);

        escalated.Should().BeFalse();
        auth.ConsecutiveRefreshFailures.Should().Be(1);
        auth.RefreshCriticalAlerted.Should().BeFalse();
        auth.DomainEvents.OfType<AccessTokenRefreshCritical>().Should().BeEmpty();
    }

    [Fact]
    public void MarkRefreshFailed_escalates_once_when_failing_inside_the_critical_window()
    {
        var auth = Create();
        var now = Expiry - TimeSpan.FromHours(1); // inside 6h window

        var first = auth.MarkRefreshFailed("boom", now, CriticalWindow);
        var second = auth.MarkRefreshFailed("boom again", now, CriticalWindow);

        first.Should().BeTrue();
        second.Should().BeFalse(); // latched — no repeat alert
        auth.ConsecutiveRefreshFailures.Should().Be(2);
        auth.RefreshCriticalAlerted.Should().BeTrue();
        auth.DomainEvents.OfType<AccessTokenRefreshCritical>().Should().ContainSingle()
            .Which.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public void Refresh_resets_failure_count_and_escalation_latch()
    {
        var auth = Create();
        var now = Expiry - TimeSpan.FromHours(1);
        auth.MarkRefreshFailed("boom", now, CriticalWindow);

        auth.Refresh([3], [4], Expiry.AddDays(30), now);

        auth.ConsecutiveRefreshFailures.Should().Be(0);
        auth.RefreshCriticalAlerted.Should().BeFalse();

        // A later failure near the new expiry can escalate again.
        var escalated = auth.MarkRefreshFailed("boom", Expiry.AddDays(30) - TimeSpan.FromHours(1), CriticalWindow);
        escalated.Should().BeTrue();
    }
}
