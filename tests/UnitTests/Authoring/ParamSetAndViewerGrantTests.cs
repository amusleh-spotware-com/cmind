using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Authoring;

// Invariants for ParamSet (create/update guards + json defaulting) and ViewerGrant create.
// (WS-1 Core backfill.)
public class ParamSetAndViewerGrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ParamSet_create_guards_name_and_defaults_blank_json()
    {
        var userId = UserId.New();
        var cbotId = CBotId.New();

        var set = ParamSet.Create(userId, cbotId, "aggressive", "  ");
        set.UserId.Should().Be(userId);
        set.CBotId.Should().Be(cbotId);
        set.Name.Should().Be("aggressive");
        set.JsonContent.Should().Be("{}", "blank json falls back to an empty object");

        var blank = () => ParamSet.Create(userId, cbotId, " ", "{}");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void ParamSet_update_mutates_and_guards()
    {
        var set = ParamSet.Create(UserId.New(), CBotId.New(), "a", "{\"x\":1}");

        set.Update("b", "{\"y\":2}");
        set.Name.Should().Be("b");
        set.JsonContent.Should().Be("{\"y\":2}");

        set.Update("c", "");
        set.JsonContent.Should().Be("{}");

        var blank = () => set.Update("", "{}");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void ViewerGrant_create_captures_who_granted_what_to_whom()
    {
        var viewer = UserId.New();
        var instance = InstanceId.New();
        var owner = UserId.New();

        var grant = ViewerGrant.Create(viewer, instance, owner, Now);

        grant.ViewerId.Should().Be(viewer);
        grant.InstanceId.Should().Be(instance);
        grant.GrantedByUserId.Should().Be(owner);
        grant.GrantedAt.Should().Be(Now);
        grant.IsDeleted.Should().BeFalse();
    }
}
