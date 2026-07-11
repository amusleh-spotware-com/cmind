using Core.Constants;
using Core.Domain;
using Core.Features;

namespace Core.Options;

/// <summary>
/// Deployment-configuration baseline for the main product features (bound from <c>App:Features</c>). Every
/// feature defaults to enabled; a white-label deployment sets the ones it does not ship to <c>false</c>.
/// </summary>
public sealed record FeaturesOptions
{
    public bool Authoring { get; init; } = true;
    public bool Backtesting { get; init; } = true;
    public bool Execution { get; init; } = true;
    public bool CopyTrading { get; init; } = true;
    public bool Ai { get; init; } = true;
    public bool PortfolioAgent { get; init; } = true;
    public bool Alerts { get; init; } = true;
    public bool PropGuard { get; init; } = true;
    public bool PropFirm { get; init; } = true;
    public bool Accounts { get; init; } = true;
    public bool OpenApi { get; init; } = true;
    public bool Mcp { get; init; } = true;
    public bool Compliance { get; init; } = true;

    public bool IsEnabled(FeatureFlag flag) => flag switch
    {
        FeatureFlag.Authoring => Authoring,
        FeatureFlag.Backtesting => Backtesting,
        FeatureFlag.Execution => Execution,
        FeatureFlag.CopyTrading => CopyTrading,
        FeatureFlag.Ai => Ai,
        FeatureFlag.PortfolioAgent => PortfolioAgent,
        FeatureFlag.Alerts => Alerts,
        FeatureFlag.PropGuard => PropGuard,
        FeatureFlag.PropFirm => PropFirm,
        FeatureFlag.Accounts => Accounts,
        FeatureFlag.OpenApi => OpenApi,
        FeatureFlag.Mcp => Mcp,
        FeatureFlag.Compliance => Compliance,
        _ => throw new DomainException(DomainErrors.FeatureFlagUnknown)
    };
}
