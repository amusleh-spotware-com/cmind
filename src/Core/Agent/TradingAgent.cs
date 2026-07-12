using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Core.Autonomy;
using Core.Constants;
using Core.Domain;

namespace Core.Agent;

/// <summary>The trading character an agent embodies; each preset fixes a sensible cadence and posture.</summary>
public enum AgentArchetype
{
    Scalper,
    DayTrader,
    SwingTrader,
    PositionTrader,
    NewsTrader,
    Contrarian,
    MeanReversion,
    BreakoutMomentum
}

/// <summary>The lifecycle state of an agent. A running agent trades until the user stops it (or it halts).</summary>
public enum AgentStatus
{
    Draft,
    Running,
    Stopped,
    Halted
}

/// <summary>Attitude axes (each 0..1) that colour how the agent reasons.</summary>
public readonly record struct AgentTemperament(double Aggressiveness, double Patience, double TrendBias)
{
    public static AgentTemperament Balanced => new(0.5, 0.5, 0.5);

    public void Validate()
    {
        if (!InRange(Aggressiveness) || !InRange(Patience) || !InRange(TrendBias))
            throw new DomainException(DomainErrors.AgentTemperamentInvalid);
        static bool InRange(double v) => !double.IsNaN(v) && v is >= 0.0 and <= 1.0;
    }
}

/// <summary>
/// A user-created, persona-driven trading agent that (when started) manages one or more live accounts
/// toward the user's goals under the Autonomy &amp; Safety Kernel. No user code — the persona is structured
/// config compiled deterministically into a system prompt. The aggregate owns its lifecycle and enforces
/// the safety invariants (Full Auto needs a risk envelope and current disclaimer consent).
/// </summary>
public sealed class TradingAgent : AuditedEntity<TradingAgentId>
{
    /// <summary>Current disclaimer version; bumping it forces every Full-Auto agent to re-consent.</summary>
    public const int CurrentDisclaimerVersion = 1;

    [MaxLength(80)] public string Name { get; private set; } = default!;
    public UserId UserId { get; private set; }
    public AgentArchetype Archetype { get; private set; }
    public AutonomyLevel Autonomy { get; private set; }
    public AgentStatus Status { get; private set; }

    public double Aggressiveness { get; private set; }
    public double Patience { get; private set; }
    public double TrendBias { get; private set; }

    public double ObjectiveDrawdownWeight { get; private set; } = 0.5;

    // Risk envelope (Full Auto), flattened; null when not set.
    public double? EnvMaxDailyLossPercent { get; private set; }
    public double? EnvMaxOpenExposureLots { get; private set; }
    public double? EnvMaxPositionSizeLots { get; private set; }
    public double? EnvMaxLeverage { get; private set; }
    public int? EnvMaxConsecutiveLosses { get; private set; }
    public int? EnvMaxOrdersPerHour { get; private set; }
    [MaxLength(1024)] public string? EnvAllowedSymbolsCsv { get; private set; }

    [MaxLength(4000)] public string GoalsJson { get; private set; } = "[]";
    [MaxLength(4000)] public string ManagedAccountsCsv { get; private set; } = string.Empty;

    public int? ConsentVersion { get; private set; }
    public DateTimeOffset? ConsentAcceptedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? LastActionAt { get; private set; }
    [MaxLength(512)] public string? LastAction { get; private set; }
    [MaxLength(256)] public string? HaltReason { get; private set; }
    public long Watermark { get; private set; }

    private TradingAgent()
    {
    }

    public static TradingAgent Create(UserId userId, string name, AgentArchetype archetype, AgentTemperament temperament)
    {
        temperament.Validate();
        return new TradingAgent
        {
            UserId = userId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.AgentNameRequired),
            Archetype = archetype,
            Autonomy = AutonomyLevel.Advisory,
            Status = AgentStatus.Draft,
            Aggressiveness = temperament.Aggressiveness,
            Patience = temperament.Patience,
            TrendBias = temperament.TrendBias,
        };
    }

    public AgentTemperament Temperament => new(Aggressiveness, Patience, TrendBias);

    public IReadOnlyList<TradingAccountId> ManagedAccounts =>
        string.IsNullOrWhiteSpace(ManagedAccountsCsv)
            ? []
            : ManagedAccountsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? TradingAccountId.From(g) : (TradingAccountId?)null)
                .Where(id => id is not null).Select(id => id!.Value).ToList();

    public IReadOnlyList<PerformanceTarget> Goals => GoalSerialization.Deserialize(GoalsJson);

    public RiskEnvelope? Envelope =>
        EnvMaxDailyLossPercent is { } dl && EnvMaxOpenExposureLots is { } oe && EnvMaxPositionSizeLots is { } ps
        && EnvMaxLeverage is { } lev && EnvMaxConsecutiveLosses is { } cl && EnvMaxOrdersPerHour is { } oph
            ? new RiskEnvelope(dl, oe, ps, lev, cl, oph, ParseSymbols(EnvAllowedSymbolsCsv))
            : null;

    public void Rename(string name) =>
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.AgentNameRequired);

    public void SetTemperament(AgentTemperament temperament)
    {
        temperament.Validate();
        Aggressiveness = temperament.Aggressiveness;
        Patience = temperament.Patience;
        TrendBias = temperament.TrendBias;
    }

    public void SetObjectiveDrawdownWeight(double weight) =>
        ObjectiveDrawdownWeight = Math.Clamp(weight, 0.0, 1.0);

    public void SetManagedAccounts(IEnumerable<TradingAccountId> accounts) =>
        ManagedAccountsCsv = string.Join(',', accounts.Select(a => a.Value.ToString()));

    public void SetGoals(IReadOnlyList<PerformanceTarget> goals) =>
        GoalsJson = GoalSerialization.Serialize(goals);

    public void SetRiskEnvelope(RiskEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        EnvMaxDailyLossPercent = envelope.MaxDailyLossPercent;
        EnvMaxOpenExposureLots = envelope.MaxOpenExposureLots;
        EnvMaxPositionSizeLots = envelope.MaxPositionSizeLots;
        EnvMaxLeverage = envelope.MaxLeverage;
        EnvMaxConsecutiveLosses = envelope.MaxConsecutiveLosses;
        EnvMaxOrdersPerHour = envelope.MaxOrdersPerHour;
        EnvAllowedSymbolsCsv = envelope.AllowedSymbols.Count > 0 ? string.Join(',', envelope.AllowedSymbols) : null;
    }

    public void SetAutonomy(AutonomyLevel level)
    {
        if (level == AutonomyLevel.FullAuto && Envelope is null)
            throw new DomainException(DomainErrors.AgentEnvelopeRequired);
        Autonomy = level;
    }

    public void AcceptDisclaimer(DateTimeOffset now)
    {
        ConsentVersion = CurrentDisclaimerVersion;
        ConsentAcceptedAt = now;
    }

    private bool HasCurrentConsent =>
        ConsentVersion is { } v && ConsentAcceptedAt is { } at
        && new DisclaimerConsent(Math.Max(1, v), at).IsCurrent(CurrentDisclaimerVersion);

    /// <summary>Starts the agent's 24/7 management. Enforces the safety pre-conditions for its autonomy level.</summary>
    public void Start(DateTimeOffset now)
    {
        if (Status is not (AgentStatus.Draft or AgentStatus.Stopped or AgentStatus.Halted))
            throw new DomainException(DomainErrors.AgentTransitionInvalid);
        if (ManagedAccounts.Count == 0)
            throw new DomainException(DomainErrors.AgentNoManagedAccounts);
        if (Autonomy == AutonomyLevel.FullAuto)
        {
            if (Envelope is null) throw new DomainException(DomainErrors.AgentEnvelopeRequired);
            if (!HasCurrentConsent) throw new DomainException(DomainErrors.AgentConsentRequired);
        }

        Status = AgentStatus.Running;
        StartedAt = now;
        HaltReason = null;
    }

    public void Stop()
    {
        if (Status is not (AgentStatus.Running or AgentStatus.Halted))
            throw new DomainException(DomainErrors.AgentTransitionInvalid);
        Status = AgentStatus.Stopped;
    }

    /// <summary>Emergency halt (kill switch / circuit breaker). Idempotent while already halted.</summary>
    public void Halt(string reason, DateTimeOffset now)
    {
        if (Status == AgentStatus.Halted) return;
        if (Status != AgentStatus.Running) throw new DomainException(DomainErrors.AgentTransitionInvalid);
        Status = AgentStatus.Halted;
        HaltReason = reason;
        LastActionAt = now;
    }

    /// <summary>Records an autonomous decision; the monotonic watermark makes replay/restart idempotent.</summary>
    public void RecordAction(string description, DateTimeOffset now)
    {
        LastAction = description;
        LastActionAt = now;
        Watermark++;
    }

    /// <summary>Deterministically compiles the persona + goals into an agent system prompt. No LLM, no I/O.</summary>
    public string CompileSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"You are '{Name}', a {Archetype} trading agent.");
        sb.Append(' ').Append(ArchetypeBrief(Archetype));
        sb.Append(CultureInfo.InvariantCulture,
            $" Temperament: aggressiveness {Aggressiveness:0.00}, patience {Patience:0.00}, trend-following {TrendBias:0.00}.");
        sb.Append(CultureInfo.InvariantCulture,
            $" Objective: weight {ObjectiveDrawdownWeight:0.00} on minimizing drawdown and {1 - ObjectiveDrawdownWeight:0.00} on maximizing return.");
        var goals = Goals;
        if (goals.Count > 0)
        {
            sb.Append(" Goals: ");
            sb.Append(string.Join("; ", goals.Select(g => string.Format(CultureInfo.InvariantCulture,
                "{0} {1} {2} ({3})", g.Metric, g.Comparator, g.Threshold, g.Enforcement))));
            sb.Append('.');
        }
        sb.Append(CultureInfo.InvariantCulture, $" Autonomy level: {Autonomy}.");
        sb.Append(" Never exceed the risk envelope; every order is validated against it before dispatch.");
        return sb.ToString();
    }

    private static string ArchetypeBrief(AgentArchetype a) => a switch
    {
        AgentArchetype.Scalper => "Trade very frequently for small, quick profits on the lowest timeframes.",
        AgentArchetype.DayTrader => "Open and close positions within the day; no overnight risk.",
        AgentArchetype.SwingTrader => "Hold positions for days to capture medium-term swings.",
        AgentArchetype.PositionTrader => "Hold for weeks, following the primary trend.",
        AgentArchetype.NewsTrader => "React to high-impact news and sentiment shifts.",
        AgentArchetype.Contrarian => "Fade crowded positioning and overextended moves.",
        AgentArchetype.MeanReversion => "Trade reversions to the mean in ranging conditions.",
        AgentArchetype.BreakoutMomentum => "Enter on breakouts and ride momentum.",
        _ => string.Empty
    };

    private static HashSet<string>? ParseSymbols(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? null
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
}

/// <summary>JSON round-trip for the agent's performance targets (stored as a column on the aggregate).</summary>
internal static class GoalSerialization
{
    private sealed record GoalDto(TargetMetric Metric, TargetComparator Comparator, double Threshold, TargetEnforcement Enforcement);

    public static string Serialize(IReadOnlyList<PerformanceTarget> goals) =>
        JsonSerializer.Serialize(goals.Select(g => new GoalDto(g.Metric, g.Comparator, g.Threshold, g.Enforcement)));

    public static IReadOnlyList<PerformanceTarget> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var dtos = JsonSerializer.Deserialize<List<GoalDto>>(json) ?? [];
            return dtos.Select(d => new PerformanceTarget(d.Metric, d.Comparator, d.Threshold, d.Enforcement)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
