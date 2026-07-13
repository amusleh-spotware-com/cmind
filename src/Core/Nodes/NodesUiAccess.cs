using Core.Constants;
using Core.Options;

namespace Core.Nodes;

/// <summary>
/// Single source of truth for how the Nodes surface is exposed, composing the white-label
/// <see cref="BrandingOptions.NodesUi"/> mode with the owner-restriction gate. Nav, the page and the
/// node endpoints all read this so the UI and the API can never disagree about what a deployment ships.
/// </summary>
public static class NodesUiAccess
{
    /// <summary>Whether the Nodes nav link and page are reachable at all (any mode except Hidden).</summary>
    public static bool IsPageVisible(NodesUiMode mode) => mode is not NodesUiMode.Hidden;

    /// <summary>Whether the manage/read node API is reachable. Hidden ⇒ the page AND the API are gone, so
    /// gating stays consistent across nav, page and endpoint (a hidden surface must not be readable by URL).
    /// Auto-discovery register/heartbeat is a separate anonymous route and is unaffected.</summary>
    public static bool IsApiReachable(NodesUiMode mode) => mode is not NodesUiMode.Hidden;

    /// <summary>Whether a node may be added or removed by hand; false ⇒ auto-discovery only.</summary>
    public static bool AllowsManualManagement(NodesUiMode mode) => mode is NodesUiMode.Full;

    /// <summary>
    /// The authorization policy that floors node access: owner-only when the deployment restricts it,
    /// otherwise the standard admin-or-above staff surface. Normal users are excluded either way.
    /// </summary>
    public static string RequiredPolicy(bool restrictToOwner)
        => restrictToOwner ? AuthPolicies.Owner : AuthPolicies.AdminOrAbove;
}
