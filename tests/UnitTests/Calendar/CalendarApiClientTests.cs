using Core;
using Core.Calendar;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

// Invariants for the CalendarApiClient aggregate: scope/name validation on create, idempotent disable,
// active/expiry evaluation, and scope checks. (WS-1 Core backfill.)
public class CalendarApiClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 21, 0, 0, TimeSpan.Zero);

    private static CalendarApiClient NewClient(DateTimeOffset? expiresAt = null) =>
        CalendarApiClient.Create(UserId.New(), "reader", [CalendarScopes.Read, CalendarScopes.Blackout],
            "cal_", "hash", expiresAt);

    [Fact]
    public void Create_stores_scopes_and_parses_them_back()
    {
        var client = NewClient();

        client.Name.Should().Be("reader");
        client.Scopes.Should().BeEquivalentTo(CalendarScopes.Read, CalendarScopes.Blackout);
        client.HasScope(CalendarScopes.Read).Should().BeTrue();
        client.HasScope(CalendarScopes.Stream).Should().BeFalse();
    }

    [Fact]
    public void Create_rejects_empty_and_unknown_scopes()
    {
        var empty = () => CalendarApiClient.Create(UserId.New(), "n", [], "p", "h", null);
        empty.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarApiScopeInvalid);

        var unknown = () => CalendarApiClient.Create(UserId.New(), "n", ["calendar:delete"], "p", "h", null);
        unknown.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarApiScopeInvalid);
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        var act = () => CalendarApiClient.Create(UserId.New(), " ", [CalendarScopes.Read], "p", "h", null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarApiClientNameRequired);
    }

    [Fact]
    public void Disable_is_idempotent_and_keeps_the_first_timestamp()
    {
        var client = NewClient();
        client.IsActive(Now).Should().BeTrue();

        client.Disable(Now);
        client.IsActive(Now).Should().BeFalse();

        client.Disable(Now.AddHours(1)); // idempotent — must not overwrite
        client.IsActive(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Is_active_respects_expiry()
    {
        var client = NewClient(expiresAt: Now.AddHours(1));

        client.IsActive(Now).Should().BeTrue();
        client.IsActive(Now.AddHours(2)).Should().BeFalse("the token has expired");
    }
}
