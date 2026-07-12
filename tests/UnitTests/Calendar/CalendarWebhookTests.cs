using Core;
using Core.Calendar;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CalendarWebhookTests
{
    private static CalendarWebhook Hook(ImpactLevel minImpact, string? currencies) =>
        CalendarWebhook.Create(UserId.New(), "https://example.com/hook", [1, 2, 3], minImpact, currencies);

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    public void Create_rejects_a_non_http_url(string url)
    {
        var act = () => CalendarWebhook.Create(UserId.New(), url, [1], ImpactLevel.Low, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarWebhookUrlInvalid);
    }

    [Fact]
    public void Matches_filters_by_impact_and_currency()
    {
        var hook = Hook(ImpactLevel.High, "USD");
        hook.Matches(ImpactLevel.Critical, ["USD"]).Should().BeTrue();
        hook.Matches(ImpactLevel.Medium, ["USD"]).Should().BeFalse();  // below minimum impact
        hook.Matches(ImpactLevel.High, ["EUR"]).Should().BeFalse();    // currency not in filter
    }

    [Fact]
    public void No_currency_filter_matches_any_currency()
    {
        Hook(ImpactLevel.Low, null).Matches(ImpactLevel.Low, ["JPY"]).Should().BeTrue();
    }

    [Fact]
    public void Disable_is_idempotent_and_flips_active()
    {
        var hook = Hook(ImpactLevel.Low, null);
        hook.IsActive.Should().BeTrue();
        hook.Disable(DateTimeOffset.UnixEpoch);
        hook.Disable(DateTimeOffset.UnixEpoch.AddDays(1));
        hook.IsActive.Should().BeFalse();
    }
}
