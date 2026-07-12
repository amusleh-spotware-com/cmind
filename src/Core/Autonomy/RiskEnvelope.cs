using System.Globalization;
using Core.Constants;
using Core.Domain;

namespace Core.Autonomy;

/// <summary>How much independence an agent has over a live account.</summary>
public enum AutonomyLevel
{
    /// <summary>Proposes only; never acts.</summary>
    Advisory,

    /// <summary>Acts only after per-action owner approval.</summary>
    ApprovalGated,

    /// <summary>Acts with no per-trade approval, bounded by the risk envelope.</summary>
    FullAuto
}

/// <summary>The outcome of checking an order against the risk envelope.</summary>
public sealed record EnvelopeDecision(bool Allowed, string? Reason)
{
    public static EnvelopeDecision Allow() => new(true, null);
    public static EnvelopeDecision Deny(string reason) => new(false, reason);
}

/// <summary>
/// The hard risk limits an autonomous agent may never breach. Every order is validated against this
/// inside the aggregate before dispatch — a breach is refused, not silently clamped. Immutable and
/// self-validating; a value object, not configuration.
/// </summary>
public sealed class RiskEnvelope
{
    public RiskEnvelope(
        double maxDailyLossPercent,
        double maxOpenExposureLots,
        double maxPositionSizeLots,
        double maxLeverage,
        int maxConsecutiveLosses,
        int maxOrdersPerHour,
        IReadOnlySet<string>? allowedSymbols = null)
    {
        if (double.IsNaN(maxDailyLossPercent) || maxDailyLossPercent is <= 0 or > 100) Invalid();
        if (!(maxOpenExposureLots > 0)) Invalid();
        if (!(maxPositionSizeLots > 0) || maxPositionSizeLots > maxOpenExposureLots) Invalid();
        if (!(maxLeverage > 0)) Invalid();
        if (maxConsecutiveLosses < 1) Invalid();
        if (maxOrdersPerHour < 1) Invalid();

        MaxDailyLossPercent = maxDailyLossPercent;
        MaxOpenExposureLots = maxOpenExposureLots;
        MaxPositionSizeLots = maxPositionSizeLots;
        MaxLeverage = maxLeverage;
        MaxConsecutiveLosses = maxConsecutiveLosses;
        MaxOrdersPerHour = maxOrdersPerHour;
        AllowedSymbols = allowedSymbols is { Count: > 0 }
            ? allowedSymbols.Select(s => s.Trim().ToUpperInvariant()).ToHashSet()
            : new HashSet<string>();
    }

    public double MaxDailyLossPercent { get; }
    public double MaxOpenExposureLots { get; }
    public double MaxPositionSizeLots { get; }
    public double MaxLeverage { get; }
    public int MaxConsecutiveLosses { get; }
    public int MaxOrdersPerHour { get; }

    /// <summary>Symbols the agent may trade; empty means no symbol restriction.</summary>
    public IReadOnlySet<string> AllowedSymbols { get; }

    /// <summary>Validates a prospective order against every per-order limit. First breach wins.</summary>
    public EnvelopeDecision CheckOrder(string symbol, double sizeLots, double openExposureLots, int ordersThisHour)
    {
        var normalized = (symbol ?? string.Empty).Trim().ToUpperInvariant();
        if (AllowedSymbols.Count > 0 && !AllowedSymbols.Contains(normalized))
            return EnvelopeDecision.Deny($"{normalized} is not in the allowed symbol list.");
        if (!(sizeLots > 0))
            return EnvelopeDecision.Deny("Order size must be positive.");
        if (sizeLots > MaxPositionSizeLots)
            return EnvelopeDecision.Deny(Fmt("Order size {0} exceeds the max position size {1}.", sizeLots, MaxPositionSizeLots));
        if (openExposureLots + sizeLots > MaxOpenExposureLots)
            return EnvelopeDecision.Deny(Fmt("Open exposure would reach {0}, above the cap {1}.", openExposureLots + sizeLots, MaxOpenExposureLots));
        if (ordersThisHour >= MaxOrdersPerHour)
            return EnvelopeDecision.Deny(Fmt("Order rate {0}/h has hit the cap {1}/h.", ordersThisHour, MaxOrdersPerHour));
        return EnvelopeDecision.Allow();
    }

    private static void Invalid() => throw new DomainException(DomainErrors.RiskEnvelopeInvalid);

    private static string Fmt(string template, double a, double b) =>
        string.Format(CultureInfo.InvariantCulture, template, a, b);
}
