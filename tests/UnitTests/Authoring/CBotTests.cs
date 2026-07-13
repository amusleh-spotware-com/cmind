using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Authoring;

// Invariants + transitions for the CBot authoring aggregate: create, rename, algo versioning, and
// owning ParamSets through the root. (WS-1 Core backfill.)
public class CBotTests
{
    private static readonly byte[] Algo = [1, 2, 3];

    private static CBot NewCBot() => CBot.Create(UserId.New(), "Scalper", Algo);

    [Fact]
    public void Create_sets_fields_and_starts_at_version_one()
    {
        var userId = UserId.New();

        var cbot = CBot.Create(userId, "Scalper", Algo);

        cbot.UserId.Should().Be(userId);
        cbot.Name.Should().Be("Scalper");
        cbot.EncryptedAlgo.Should().BeEquivalentTo(Algo);
        cbot.Version.Should().Be(1);
        cbot.ParamSets.Should().BeEmpty();
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        var act = () => CBot.Create(UserId.New(), "  ", Algo);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Rename_changes_the_name()
    {
        var cbot = NewCBot();

        cbot.Rename("Breakout");

        cbot.Name.Should().Be("Breakout");
    }

    [Fact]
    public void Rename_rejects_a_blank_name()
    {
        var cbot = NewCBot();
        var act = () => cbot.Rename("");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Update_algo_replaces_bytes_and_bumps_the_version()
    {
        var cbot = NewCBot();
        byte[] next = [9, 9];

        cbot.UpdateAlgo(next);

        cbot.EncryptedAlgo.Should().BeEquivalentTo(next);
        cbot.Version.Should().Be(2);
        cbot.SourceProjectId.Should().BeNull("no project id was supplied");
    }

    [Fact]
    public void Update_algo_sets_the_source_project_when_supplied()
    {
        var cbot = NewCBot();
        var projectId = CBotSourceProjectId.New();

        cbot.UpdateAlgo([4, 5], projectId);

        cbot.SourceProjectId.Should().Be(projectId);
        cbot.Version.Should().Be(2);
    }

    [Fact]
    public void Add_param_set_owns_it_through_the_root()
    {
        var cbot = NewCBot();

        var paramSet = cbot.AddParamSet("aggressive", "{\"risk\":2}");

        cbot.ParamSets.Should().ContainSingle().Which.Should().BeSameAs(paramSet);
        paramSet.UserId.Should().Be(cbot.UserId);
        paramSet.CBotId.Should().Be(cbot.Id);
        paramSet.Name.Should().Be("aggressive");
    }
}
