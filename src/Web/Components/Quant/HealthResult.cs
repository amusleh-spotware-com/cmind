namespace Web.Components.Quant;

/// <summary>
/// Client-side projection of the Strategy Health &amp; Alpha Decay report (server: <c>HealthResponse</c>).
/// Shared by the Health page, the reusable <c>HealthResultView</c>, and the backtest-instance health
/// dialog so the verdict and statistics render identically wherever the check is run.
/// </summary>
public sealed record HealthResult(
    string health,
    double earlierSharpe,
    double recentSharpe,
    int? changePointIndex,
    int observations,
    string rationale);
