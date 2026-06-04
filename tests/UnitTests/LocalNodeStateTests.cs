using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class LocalNodeStateTests
{
    [Fact]
    public void Disabled_LocalNode_is_not_active()
    {
        var n = new LocalNode { Enabled = false };
        n.IsActive.Should().BeFalse();
        n.AcceptsRun.Should().BeFalse();
        n.AcceptsBacktest.Should().BeFalse();
        n.StatusName.Should().Be("Disabled");
        n.IsLocal.Should().BeTrue();
    }

    [Fact]
    public void Enabled_LocalNode_accepts_run_and_backtest()
    {
        var n = new LocalNode { Enabled = true };
        n.IsActive.Should().BeTrue();
        n.AcceptsRun.Should().BeTrue();
        n.AcceptsBacktest.Should().BeTrue();
        n.StatusName.Should().Be("Active");
        n.ModeName.Should().Be("Mixed");
    }

    [Fact]
    public void RemoteNode_IsLocal_false()
    {
        new ActiveMixedNode().IsLocal.Should().BeFalse();
        new ActiveRunNode().IsLocal.Should().BeFalse();
    }
}
