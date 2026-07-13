using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

// The strongly-typed state records: FromName parsing (case-insensitive + unknown throws), role ranking,
// and the InstanceStatus terminal/active flags. (WS-1 Core backfill.)
public class StateTypesTests
{
    [Fact]
    public void UserRole_from_name_is_case_insensitive_and_ranks_correctly()
    {
        UserRole.FromName("owner").Should().Be(UserRole.Owner);
        UserRole.FromName("VIEWER").Should().Be(UserRole.Viewer);

        UserRole.Owner.IsAtLeast(UserRole.Admin).Should().BeTrue("owner outranks admin");
        UserRole.Viewer.IsAtLeast(UserRole.Owner).Should().BeFalse("viewer does not outrank owner");

        var unknown = () => UserRole.FromName("superuser");
        unknown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Node_mode_status_and_instance_type_round_trip_by_name()
    {
        NodeMode.FromName("run").Should().Be(NodeMode.Run);
        NodeStatus.FromName("unreachable").Should().Be(NodeStatus.Unreachable);
        InstanceType.FromName("backtest").Should().Be(InstanceType.Backtest);

        var badMode = () => NodeMode.FromName("nope");
        badMode.Should().Throw<ArgumentException>();
        var badStatus = () => NodeStatus.FromName("nope");
        badStatus.Should().Throw<ArgumentException>();
        var badType = () => InstanceType.FromName("nope");
        badType.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Instance_status_exposes_terminal_and_active_flags()
    {
        InstanceStatus.Running.IsActive.Should().BeTrue();
        InstanceStatus.Running.IsTerminal.Should().BeFalse();

        InstanceStatus.Completed.IsTerminal.Should().BeTrue();
        InstanceStatus.Completed.IsActive.Should().BeFalse();

        InstanceStatus.FromName("failed").Should().Be(InstanceStatus.Failed);
        var bad = () => InstanceStatus.FromName("nope");
        bad.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CBot_language_round_trips_and_carries_its_extension()
    {
        CBotLanguage.FromName("csharp").Should().Be(CBotLanguage.CSharp);
        CBotLanguage.CSharp.FileExtension.Should().Be(".cs");
        CBotLanguage.Python.FileExtension.Should().Be(".py");
    }
}
