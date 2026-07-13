using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// Fills the CopyProfile paths not covered by CopyProfileTests: rename, destination removal guards, the
// node-lease lifecycle, flatten request, and the error transition. (WS-1 Core backfill.)
public class CopyProfileLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly NodeIdentity Node = new("node-a");

    private static CopyProfile NewProfile() => CopyProfile.Create(UserId.New(), "profile", TradingAccountId.New());

    [Fact]
    public void Rename_changes_the_name_and_rejects_blank()
    {
        var profile = NewProfile();

        profile.Rename("renamed");
        profile.Name.Should().Be("renamed");

        var act = () => profile.Rename("  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Remove_destination_is_noop_when_missing_blocked_when_locked_then_removes()
    {
        var profile = NewProfile();
        profile.RemoveDestination(CopyDestinationId.New(), Now); // missing -> no throw, no change
        profile.Destinations.Should().BeEmpty();

        var dest = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        dest.LockConfig(Now.AddHours(1));

        var locked = () => profile.RemoveDestination(dest.Id, Now);
        locked.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CopyDestinationConfigLocked);

        profile.RemoveDestination(dest.Id, Now.AddHours(2)); // lock expired -> removes
        profile.Destinations.Should().BeEmpty();
    }

    [Fact]
    public void Lease_lifecycle_claims_renews_and_releases()
    {
        var profile = NewProfile();

        profile.ClaimBy(Node, Now.AddMinutes(5));
        profile.IsHostedBy(Node).Should().BeTrue();
        profile.IsLeaseHeldBy(Node, Now).Should().BeTrue();
        profile.IsLeaseHeldBy(Node, Now.AddMinutes(6)).Should().BeFalse("the lease has lapsed");

        profile.RenewLease(Now.AddMinutes(10));
        profile.IsLeaseHeldBy(Node, Now.AddMinutes(6)).Should().BeTrue("renewal extended the lease");

        profile.ReleaseAssignment();
        profile.IsHostedBy(Node).Should().BeFalse();
        profile.IsLeaseHeldBy(Node, Now).Should().BeFalse();
    }

    [Fact]
    public void Flatten_request_is_set_and_cleared()
    {
        var profile = NewProfile();

        profile.RequestFlatten(Now);
        profile.FlattenRequestedAt.Should().Be(Now);

        profile.ClearFlattenRequest();
        profile.FlattenRequestedAt.Should().BeNull();
    }

    [Fact]
    public void Mark_error_moves_to_error_state()
    {
        var profile = NewProfile();
        profile.Start();

        profile.MarkError("boom");

        profile.Status.Should().Be(CopyProfileStatus.Error);
    }
}
