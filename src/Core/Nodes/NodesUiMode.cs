namespace Core.Nodes;

/// <summary>
/// How much of the Nodes surface a white-label deployment exposes (bound from
/// <c>App:Branding:NodesUi</c>). Nodes are infrastructure most tenants never manage by hand — agents
/// self-register and heartbeat (<see cref="Core.Constants.NodeDiscoveryRoutes.Register"/>), so a reseller
/// can hide the manual controls or the page entirely and still run a healthy cluster via auto-discovery.
/// </summary>
public enum NodesUiMode
{
    /// <summary>Full control: the list plus manual add and delete. The stock product default.</summary>
    Full,

    /// <summary>Read-only monitoring: the list and stats stay, but manual add and delete are removed —
    /// nodes only ever appear through auto-discovery.</summary>
    Monitor,

    /// <summary>The Nodes nav link, page and manual API are all gone; the cluster is auto-discovery only.</summary>
    Hidden
}
