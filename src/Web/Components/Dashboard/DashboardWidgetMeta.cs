using Core.Dashboard;
using Core.Features;
using MudBlazor;

namespace Web.Components.Dashboard;

/// <summary>UI-side presentation for each dashboard widget key (label + icon) plus the rule for whether a
/// given deployment/user can use it. Keeps the Index renderer and the customize dialog in agreement, and
/// keys everything off the Core <see cref="DashboardWidgets"/> catalog so there is one source of truth.</summary>
public static class DashboardWidgetMeta
{
    public sealed record Meta(string Key, string Label, string Icon);

    private static readonly Dictionary<string, Meta> ByKey = new(StringComparer.Ordinal)
    {
        [DashboardWidgets.Kpis] = new(DashboardWidgets.Kpis, "Key metrics", Icons.Material.Filled.Speed),
        [DashboardWidgets.ActivityChart] = new(DashboardWidgets.ActivityChart, "Activity chart", Icons.Material.Filled.ShowChart),
        [DashboardWidgets.StatusRing] = new(DashboardWidgets.StatusRing, "Status breakdown", Icons.Material.Filled.DonutLarge),
        [DashboardWidgets.ActivityFeed] = new(DashboardWidgets.ActivityFeed, "Live activity", Icons.Material.Filled.Timeline),
        [DashboardWidgets.Backtests] = new(DashboardWidgets.Backtests, "Backtests", Icons.Material.Filled.History),
        [DashboardWidgets.CopyProfiles] = new(DashboardWidgets.CopyProfiles, "Copy trading", Icons.Material.Filled.Repeat),
        [DashboardWidgets.Agents] = new(DashboardWidgets.Agents, "AI agents", Icons.Material.Filled.SmartToy),
        [DashboardWidgets.Resources] = new(DashboardWidgets.Resources, "My resources", Icons.Material.Filled.Inventory2),
        [DashboardWidgets.NodeHealth] = new(DashboardWidgets.NodeHealth, "Node health", Icons.Material.Filled.Dns)
    };

    public static Meta For(string key) =>
        ByKey.TryGetValue(key, out var meta) ? meta : new Meta(key, key, Icons.Material.Filled.Widgets);

    /// <summary>Whether the widget is usable for this user/deployment. Feature-gated widgets and the
    /// admin-only node-health widget are dropped from both the board and the customize dialog otherwise.</summary>
    public static bool IsAvailable(string key, IFeatureGate gate, bool isAdmin) => key switch
    {
        DashboardWidgets.NodeHealth => isAdmin,
        DashboardWidgets.CopyProfiles => gate.IsEnabled(FeatureFlag.CopyTrading),
        DashboardWidgets.Agents => gate.IsEnabled(FeatureFlag.AgentStudio),
        DashboardWidgets.Backtests => gate.IsEnabled(FeatureFlag.Backtesting),
        _ => true
    };
}
