using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Dashboard;

/// <summary>A user's chosen preference for one dashboard widget: whether it shows and where it sits.</summary>
public readonly record struct DashboardWidgetPreference(string Key, bool Visible);

/// <summary>One widget's persisted placement, owned by the <see cref="UserDashboard"/> aggregate.</summary>
public sealed class DashboardWidgetSetting
{
    [MaxLength(64)] public string Key { get; private set; } = default!;
    public bool Visible { get; private set; }
    public int Order { get; private set; }

    private DashboardWidgetSetting()
    {
    }

    internal DashboardWidgetSetting(string key, bool visible, int order)
    {
        Key = key;
        Visible = visible;
        Order = order;
    }
}

/// <summary>The per-user dashboard layout: which widgets are shown and in what order. A user has at most
/// one board (unique on <see cref="UserId"/>). Rich aggregate — the ordered widget list is only ever
/// mutated through <see cref="Apply"/> / <see cref="Reset"/>, which validate every key against the
/// <see cref="DashboardWidgets"/> catalog and keep the collection complete and de-duplicated so the UI can
/// render it directly.</summary>
public sealed class UserDashboard : AuditedEntity<UserDashboardId>
{
    private readonly List<DashboardWidgetSetting> _widgets = [];

    public UserId UserId { get; private set; }

    public IReadOnlyList<DashboardWidgetSetting> Widgets => _widgets;

    private UserDashboard()
    {
    }

    /// <summary>A fresh board seeded with the catalog defaults (every widget visible, canonical order).</summary>
    public static UserDashboard CreateDefault(UserId userId)
    {
        var board = new UserDashboard { UserId = userId };
        board.SeedDefaults();
        return board;
    }

    /// <summary>Replaces the layout with the caller's ordered preferences. Unknown keys are rejected; any
    /// catalog widget the caller omitted is appended (visible) so the board always covers the full catalog
    /// even after new widgets ship. Duplicate keys collapse to their first occurrence.</summary>
    public void Apply(IReadOnlyList<DashboardWidgetPreference> preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        foreach (var preference in preferences)
        {
            if (!DashboardWidgets.IsKnown(preference.Key))
                throw new DomainException(DomainErrors.DashboardWidgetUnknown, $"Unknown dashboard widget '{preference.Key}'.");
        }

        _widgets.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var preference in preferences)
        {
            if (!seen.Add(preference.Key)) continue;
            _widgets.Add(new DashboardWidgetSetting(preference.Key, preference.Visible, order++));
        }

        foreach (var key in DashboardWidgets.DefaultOrder)
        {
            if (seen.Add(key)) _widgets.Add(new DashboardWidgetSetting(key, true, order++));
        }
    }

    /// <summary>Restores the catalog default layout.</summary>
    public void Reset()
    {
        _widgets.Clear();
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        var order = 0;
        foreach (var key in DashboardWidgets.DefaultOrder)
            _widgets.Add(new DashboardWidgetSetting(key, true, order++));
    }
}
