using Core.Constants;
using Core.Nodes;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.Nodes;

public sealed class NodesUiAccessTests
{
    [Theory]
    [InlineData(NodesUiMode.Full, true)]
    [InlineData(NodesUiMode.Monitor, true)]
    [InlineData(NodesUiMode.Hidden, false)]
    public void Page_is_visible_in_every_mode_except_hidden(NodesUiMode mode, bool expected)
    {
        NodesUiAccess.IsPageVisible(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(NodesUiMode.Full, true)]
    [InlineData(NodesUiMode.Monitor, false)]
    [InlineData(NodesUiMode.Hidden, false)]
    public void Manual_management_is_allowed_only_in_full(NodesUiMode mode, bool expected)
    {
        NodesUiAccess.AllowsManualManagement(mode).Should().Be(expected);
    }

    [Fact]
    public void Required_policy_is_owner_only_when_restricted_else_admin_or_above()
    {
        NodesUiAccess.RequiredPolicy(restrictToOwner: true).Should().Be(AuthPolicies.Owner);
        NodesUiAccess.RequiredPolicy(restrictToOwner: false).Should().Be(AuthPolicies.AdminOrAbove);
    }

    [Fact]
    public void Default_branding_ships_the_full_nodes_surface_to_all_staff()
    {
        var branding = new BrandingOptions();
        branding.NodesUi.Should().Be(NodesUiMode.Full);
        branding.RestrictNodesToOwner.Should().BeFalse();
        NodesUiAccess.IsPageVisible(branding.NodesUi).Should().BeTrue();
        NodesUiAccess.AllowsManualManagement(branding.NodesUi).Should().BeTrue();
    }
}
