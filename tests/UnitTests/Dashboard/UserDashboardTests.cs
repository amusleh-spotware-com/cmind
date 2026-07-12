using Core;
using Core.Constants;
using Core.Dashboard;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Dashboard;

public sealed class UserDashboardTests
{
    [Fact]
    public void CreateDefault_seeds_the_whole_catalog_visible_in_canonical_order()
    {
        var board = UserDashboard.CreateDefault(UserId.New());

        board.Widgets.Select(w => w.Key).Should().Equal(DashboardWidgets.DefaultOrder);
        board.Widgets.Should().OnlyContain(w => w.Visible);
        board.Widgets.Select(w => w.Order).Should().Equal(Enumerable.Range(0, DashboardWidgets.DefaultOrder.Count));
    }

    [Fact]
    public void Apply_stores_the_given_order_and_visibility()
    {
        var board = UserDashboard.CreateDefault(UserId.New());

        board.Apply(
        [
            new DashboardWidgetPreference(DashboardWidgets.Agents, true),
            new DashboardWidgetPreference(DashboardWidgets.Kpis, false)
        ]);

        board.Widgets[0].Key.Should().Be(DashboardWidgets.Agents);
        board.Widgets[0].Order.Should().Be(0);
        board.Widgets[1].Key.Should().Be(DashboardWidgets.Kpis);
        board.Widgets[1].Visible.Should().BeFalse();
    }

    [Fact]
    public void Apply_appends_omitted_catalog_widgets_as_visible_at_the_end()
    {
        var board = UserDashboard.CreateDefault(UserId.New());

        board.Apply([new DashboardWidgetPreference(DashboardWidgets.Backtests, false)]);

        board.Widgets[0].Key.Should().Be(DashboardWidgets.Backtests);
        board.Widgets.Select(w => w.Key).Should().BeEquivalentTo(DashboardWidgets.DefaultOrder);
        board.Widgets.Skip(1).Should().OnlyContain(w => w.Visible, "omitted widgets default to visible");
        board.Widgets.Select(w => w.Order).Should().Equal(Enumerable.Range(0, DashboardWidgets.DefaultOrder.Count));
    }

    [Fact]
    public void Apply_collapses_duplicate_keys_to_the_first()
    {
        var board = UserDashboard.CreateDefault(UserId.New());

        board.Apply(
        [
            new DashboardWidgetPreference(DashboardWidgets.Kpis, false),
            new DashboardWidgetPreference(DashboardWidgets.Kpis, true)
        ]);

        board.Widgets.Count(w => w.Key == DashboardWidgets.Kpis).Should().Be(1);
        board.Widgets[0].Visible.Should().BeFalse("the first occurrence wins");
    }

    [Fact]
    public void Apply_rejects_an_unknown_widget_key()
    {
        var board = UserDashboard.CreateDefault(UserId.New());

        var act = () => board.Apply([new DashboardWidgetPreference("not-a-widget", true)]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.DashboardWidgetUnknown);
    }

    [Fact]
    public void Reset_restores_the_default_layout()
    {
        var board = UserDashboard.CreateDefault(UserId.New());
        board.Apply([new DashboardWidgetPreference(DashboardWidgets.Agents, false)]);

        board.Reset();

        board.Widgets.Select(w => w.Key).Should().Equal(DashboardWidgets.DefaultOrder);
        board.Widgets.Should().OnlyContain(w => w.Visible);
    }
}
