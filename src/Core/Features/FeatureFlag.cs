namespace Core.Features;

/// <summary>
/// The main, independently deployable product features that a white-label deployment can turn on or off.
/// Every flag defaults to enabled; a deployment disables a feature via <c>App:Features:&lt;Flag&gt;=false</c>
/// and an owner may flip it later at runtime. Core admin surfaces (dashboard, users, nodes, auth) are not
/// gateable and never appear here.
/// </summary>
public enum FeatureFlag
{
    Authoring,
    Backtesting,
    Execution,
    CopyTrading,
    Ai,
    PortfolioAgent,
    AgentStudio,
    Alerts,
    PropGuard,
    PropFirm,
    Accounts,
    OpenApi,
    Mcp,
    Compliance,
    Registration,
    EconomicCalendar
}
