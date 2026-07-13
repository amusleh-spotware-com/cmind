using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

// Invariants for the McpApiKey aggregate: creation label guard, usage stamping, single revocation.
// (WS-1 Core backfill.)
public class McpApiKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 8, 30, 0, TimeSpan.Zero);

    private static McpApiKey NewKey() => McpApiKey.Create(UserId.New(), "cm_live_", "hash", "CI key");

    [Fact]
    public void Create_sets_fields()
    {
        var key = NewKey();
        key.KeyPrefix.Should().Be("cm_live_");
        key.Label.Should().Be("CI key");
        key.RevokedAt.Should().BeNull();
        key.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_a_blank_label()
    {
        var act = () => McpApiKey.Create(UserId.New(), "p", "h", "  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Mark_used_stamps_the_time()
    {
        var key = NewKey();
        key.MarkUsed(Now);
        key.LastUsedAt.Should().Be(Now);
    }

    [Fact]
    public void Revoke_sets_the_timestamp_once_and_rejects_a_second_revoke()
    {
        var key = NewKey();

        key.Revoke(Now);
        key.RevokedAt.Should().Be(Now);

        var again = () => key.Revoke(Now.AddMinutes(1));
        again.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.McpKeyAlreadyRevoked);
    }
}
