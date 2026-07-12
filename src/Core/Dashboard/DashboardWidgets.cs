namespace Core.Dashboard;

/// <summary>The canonical catalog of dashboard widgets. A single source of truth shared by the
/// <see cref="UserDashboard"/> aggregate (default layout + key validation) and the Web UI (rendering
/// and the customize dialog). Keys are stable wire strings — never rename one without a data migration.</summary>
public static class DashboardWidgets
{
    public const string Kpis = "kpis";
    public const string ActivityChart = "activity-chart";
    public const string StatusRing = "status-ring";
    public const string ActivityFeed = "activity-feed";
    public const string Backtests = "backtests";
    public const string CopyProfiles = "copy-profiles";
    public const string Agents = "agents";
    public const string Resources = "resources";
    public const string NodeHealth = "node-health";

    /// <summary>Default widget order, all visible, applied to a user who has never customized their board.</summary>
    public static readonly IReadOnlyList<string> DefaultOrder =
    [
        Kpis, ActivityChart, StatusRing, ActivityFeed, Backtests, CopyProfiles, Agents, Resources, NodeHealth
    ];

    /// <summary>Widgets only meaningful to an administrator (cluster-wide node capacity). Hidden for others.</summary>
    public static readonly IReadOnlySet<string> AdminOnly = new HashSet<string>(StringComparer.Ordinal) { NodeHealth };

    private static readonly IReadOnlySet<string> Known = new HashSet<string>(DefaultOrder, StringComparer.Ordinal);

    public static bool IsKnown(string key) => Known.Contains(key);

    public static bool IsAdminOnly(string key) => AdminOnly.Contains(key);
}
