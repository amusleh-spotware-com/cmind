using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;
using Core.PropFirm;

namespace Core;

/// <summary>
/// A prop-firm evaluation account: tracks equity against the firm's rules (profit target, max daily loss,
/// max total drawdown, minimum trading days) and drives the phase/state machine
/// Evaluation -&gt; Verification -&gt; Funded, or Failed on a breach. The aggregate owns the rule evaluation:
/// callers feed it equity snapshots via <see cref="RecordEquity"/> and it decides every transition.
/// </summary>
public sealed class PropFirmChallenge : AuditedEntity<PropFirmChallengeId>
{
    public UserId UserId { get; private set; }
    public TradingAccountId TradingAccountId { get; private set; }
    [MaxLength(128)] public string Name { get; private set; } = default!;

    public decimal StartingBalance { get; private set; }
    public double ProfitTargetPercent { get; private set; }
    public double MaxDailyLossPercent { get; private set; }
    public double MaxTotalDrawdownPercent { get; private set; }
    public DrawdownMode DrawdownMode { get; private set; }
    public int MinTradingDays { get; private set; }
    public bool SingleStep { get; private set; }

    public ChallengeKind Kind { get; private set; }
    public DailyLossBasis DailyLossBasis { get; private set; }
    public decimal TrailingThresholdAmount { get; private set; }
    public decimal TrailingLockThreshold { get; private set; }
    public double? ConsistencyMaxDayProfitSharePercent { get; private set; }
    public int? MaxCalendarDays { get; private set; }
    public int? MaxInactivityDays { get; private set; }
    public int? MaxOpenPositions { get; private set; }
    public bool AllowWeekendHolding { get; private set; }
    public bool AllowNewsTrading { get; private set; }
    public double DrawdownWarnThresholdPercent { get; private set; }

    public ChallengePhase Phase { get; private set; }
    public ChallengeStatus Status { get; private set; }
    public BreachReason Breach { get; private set; }

    public decimal CurrentEquity { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public decimal PeakEquity { get; private set; }
    public decimal DailyStartEquity { get; private set; }
    public decimal DailyStartBalance { get; private set; }
    public decimal MaxSingleDayProfit { get; private set; }
    public DateOnly? CurrentDay { get; private set; }
    public int TradingDaysCount { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? LastEquityAt { get; private set; }
    public DateTimeOffset? LastActivityAt { get; private set; }
    public bool DrawdownWarned { get; private set; }

    // Node lease — a tracking node claims an active challenge for a bounded lease and renews it while alive;
    // if the node dies the lease lapses and any other node can reclaim it (mirrors CopyProfile self-healing).
    [MaxLength(64)] public string? AssignedNode { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    private PropFirmChallenge()
    {
    }

    public ChallengeRules Rules => new(
        new Percent(ProfitTargetPercent),
        new Percent(MaxDailyLossPercent),
        new Percent(MaxTotalDrawdownPercent),
        DrawdownMode,
        new TradingDayRequirement(MinTradingDays),
        SingleStep)
    {
        Kind = Kind,
        DailyLossBasis = DailyLossBasis,
        TrailingThresholdAmount = TrailingThresholdAmount,
        TrailingLockThreshold = TrailingLockThreshold,
        ConsistencyMaxDayProfitSharePercent = ConsistencyMaxDayProfitSharePercent,
        MaxCalendarDays = MaxCalendarDays,
        MaxInactivityDays = MaxInactivityDays,
        MaxOpenPositions = MaxOpenPositions,
        AllowWeekendHolding = AllowWeekendHolding,
        AllowNewsTrading = AllowNewsTrading
    };

    public static PropFirmChallenge Create(UserId userId, TradingAccountId tradingAccountId, string name,
        Money startingBalance, ChallengeRules rules)
    {
        if (startingBalance.Amount <= 0) throw new DomainException(DomainErrors.PropFirmStartingBalanceInvalid);
        if (rules.DrawdownMode == DrawdownMode.TrailingThreshold && rules.TrailingThresholdAmount <= 0)
            throw new DomainException(DomainErrors.PropFirmDrawdownThresholdInvalid);

        var challenge = new PropFirmChallenge
        {
            UserId = userId,
            TradingAccountId = tradingAccountId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            StartingBalance = startingBalance.Amount,
            ProfitTargetPercent = rules.ProfitTarget.Value,
            MaxDailyLossPercent = rules.MaxDailyLoss.Value,
            MaxTotalDrawdownPercent = rules.MaxTotalDrawdown.Value,
            DrawdownMode = rules.DrawdownMode,
            MinTradingDays = rules.MinTradingDays.Value,
            SingleStep = rules.SingleStep,
            Kind = rules.Kind,
            DailyLossBasis = rules.DailyLossBasis,
            TrailingThresholdAmount = rules.TrailingThresholdAmount,
            TrailingLockThreshold = rules.TrailingLockThreshold,
            ConsistencyMaxDayProfitSharePercent = rules.ConsistencyMaxDayProfitSharePercent,
            MaxCalendarDays = rules.MaxCalendarDays,
            MaxInactivityDays = rules.MaxInactivityDays,
            MaxOpenPositions = rules.MaxOpenPositions,
            AllowWeekendHolding = rules.AllowWeekendHolding,
            AllowNewsTrading = rules.AllowNewsTrading,
            Phase = ChallengePhase.Evaluation,
            Status = ChallengeStatus.Active,
            Breach = BreachReason.None,
            CurrentEquity = startingBalance.Amount,
            CurrentBalance = startingBalance.Amount,
            PeakEquity = startingBalance.Amount,
            DailyStartEquity = startingBalance.Amount,
            DailyStartBalance = startingBalance.Amount
        };

        challenge.RaiseDomainEvent(new PropFirmChallengeStarted(challenge.Id, userId));
        return challenge;
    }

    public void Rename(string name) =>
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);

    /// <summary>Sets the equity-usage percentage at which a soft drawdown warning is raised (0 disables it).</summary>
    public void SetDrawdownWarnThreshold(double percentUsed) =>
        DrawdownWarnThresholdPercent = percentUsed is >= 0 and <= 100 ? percentUsed : 0;

    /// <summary>Stops tracking the challenge without passing or failing it (Active -&gt; Stopped).</summary>
    public void Stop()
    {
        if (Status != ChallengeStatus.Active) throw new DomainException(DomainErrors.PropFirmChallengeTransitionInvalid);
        Status = ChallengeStatus.Stopped;
        ReleaseAssignment();
        RaiseDomainEvent(new PropFirmChallengeStopped(Id, UserId));
    }

    /// <summary>Resumes a stopped challenge (Stopped -&gt; Active), continuing from its current state.</summary>
    public void Resume()
    {
        if (Status != ChallengeStatus.Stopped) throw new DomainException(DomainErrors.PropFirmChallengeTransitionInvalid);
        Status = ChallengeStatus.Active;
        RaiseDomainEvent(new PropFirmChallengeStarted(Id, UserId));
    }

    public bool IsHostedBy(NodeIdentity node) => string.Equals(AssignedNode, node.Value, StringComparison.Ordinal);

    public void ClaimBy(NodeIdentity node, DateTimeOffset leaseUntil)
    {
        AssignedNode = node.Value;
        LeaseExpiresAt = leaseUntil;
    }

    public void RenewLease(DateTimeOffset leaseUntil) => LeaseExpiresAt = leaseUntil;

    public bool IsLeaseHeldBy(NodeIdentity node, DateTimeOffset now)
        => string.Equals(AssignedNode, node.Value, StringComparison.Ordinal) && LeaseExpiresAt > now;

    public void ReleaseAssignment()
    {
        AssignedNode = null;
        LeaseExpiresAt = null;
    }

    /// <summary>Convenience overload — records an equity reading where balance equals equity (no open P&amp;L).</summary>
    public void RecordEquity(Money equity, DateTimeOffset now) =>
        RecordEquity(new EquitySnapshot(equity, equity), now);

    /// <summary>
    /// Feeds an equity snapshot and evaluates every rule: rolls the trading day, updates peaks, fails the
    /// challenge on the first breach, or advances the phase when the profit target, minimum-trading-day and
    /// consistency requirements are met. Only valid while the challenge is <see cref="ChallengeStatus.Active"/>.
    /// </summary>
    public void RecordEquity(EquitySnapshot snapshot, DateTimeOffset now)
    {
        if (Status != ChallengeStatus.Active) throw new DomainException(DomainErrors.PropFirmChallengeNotActive);
        if (LastEquityAt is { } last && now < last)
            throw new DomainException(DomainErrors.PropFirmEquityOutOfOrder);

        StartedAt ??= now;

        var day = DateOnly.FromDateTime(now.UtcDateTime);
        if (CurrentDay != day)
        {
            if (CurrentDay is not null)
            {
                var previousDayProfit = CurrentEquity - DailyStartEquity;
                if (previousDayProfit > MaxSingleDayProfit) MaxSingleDayProfit = previousDayProfit;
            }

            CurrentDay = day;
            DailyStartEquity = CurrentEquity;
            DailyStartBalance = CurrentBalance;
            TradingDaysCount++;
        }

        CurrentEquity = snapshot.Equity.Amount;
        CurrentBalance = snapshot.Balance.Amount;
        if (CurrentEquity > PeakEquity) PeakEquity = CurrentEquity;
        LastEquityAt = now;

        RaiseDrawdownWarningIfNeeded();

        if (TryBreach(now)) return;
        TryPassPhase();
    }

    /// <summary>
    /// Feeds non-equity trading facts (open exposure, news-window activity, weekend holding) and fails the
    /// challenge if a behaviour rule is breached. Also stamps activity for the inactivity rule.
    /// </summary>
    public void RecordActivity(ActivitySnapshot activity, DateTimeOffset now)
    {
        if (Status != ChallengeStatus.Active) throw new DomainException(DomainErrors.PropFirmChallengeNotActive);
        LastActivityAt = now;

        if (MaxOpenPositions is { } cap && activity.OpenPositions > cap)
        {
            Fail(BreachReason.MaxExposure);
            return;
        }

        if (!AllowWeekendHolding && activity.HoldingOverWeekend)
        {
            Fail(BreachReason.WeekendHolding);
            return;
        }

        if (!AllowNewsTrading && activity.OpenedInNewsWindow)
            Fail(BreachReason.NewsTrading);
    }

    private void RaiseDrawdownWarningIfNeeded()
    {
        if (DrawdownWarned || DrawdownWarnThresholdPercent <= 0) return;

        var used = DrawdownUsedPercent();
        if (used < DrawdownWarnThresholdPercent) return;

        DrawdownWarned = true;
        RaiseDomainEvent(new PropFirmDrawdownWarning(Id, UserId, used));
    }

    private double DrawdownUsedPercent()
    {
        var reference = DrawdownMode == DrawdownMode.Static ? StartingBalance : PeakEquity;
        if (reference <= 0) return 0;
        var loss = reference - CurrentEquity;
        if (loss <= 0) return 0;
        var maxLoss = reference * (decimal)MaxTotalDrawdownPercent / 100m;
        return maxLoss <= 0 ? 0 : (double)(loss / maxLoss * 100m);
    }

    private bool TryBreach(DateTimeOffset now)
    {
        if (Rules.DailyLoss().IsBreached(DailyStartEquity, CurrentEquity, DailyStartBalance, CurrentBalance))
        {
            Fail(BreachReason.DailyLoss);
            return true;
        }

        if (Rules.Drawdown().IsBreached(StartingBalance, PeakEquity, CurrentEquity))
        {
            Fail(BreachReason.MaxDrawdown);
            return true;
        }

        if (MaxCalendarDays is { } maxDays && StartedAt is { } started && now - started > TimeSpan.FromDays(maxDays))
        {
            Fail(BreachReason.TimeLimit);
            return true;
        }

        if (MaxInactivityDays is { } maxIdle && LastActivityAt is { } lastActivity
            && now - lastActivity > TimeSpan.FromDays(maxIdle))
        {
            Fail(BreachReason.Inactivity);
            return true;
        }

        return false;
    }

    private void TryPassPhase()
    {
        var target = StartingBalance * (decimal)ProfitTargetPercent / 100m;
        var totalProfit = CurrentEquity - StartingBalance;
        if (totalProfit < target) return;
        if (TradingDaysCount < MinTradingDays) return;

        if (Rules.Consistency() is { } consistency)
        {
            var currentDayProfit = CurrentEquity - DailyStartEquity;
            var effectiveMaxDayProfit = currentDayProfit > MaxSingleDayProfit ? currentDayProfit : MaxSingleDayProfit;
            if (!consistency.IsSatisfied(effectiveMaxDayProfit, totalProfit)) return;
        }

        if (Phase == ChallengePhase.Evaluation && !SingleStep)
        {
            Phase = ChallengePhase.Verification;
            ResetBaseline();
            RaiseDomainEvent(new PropFirmPhasePassed(Id, UserId, Phase));
            return;
        }

        Phase = ChallengePhase.Funded;
        Status = ChallengeStatus.Passed;
        ReleaseAssignment();
        RaiseDomainEvent(new PropFirmPhasePassed(Id, UserId, Phase));
        RaiseDomainEvent(new PropFirmChallengePassed(Id, UserId));
    }

    private void ResetBaseline()
    {
        CurrentEquity = StartingBalance;
        CurrentBalance = StartingBalance;
        PeakEquity = StartingBalance;
        DailyStartEquity = StartingBalance;
        DailyStartBalance = StartingBalance;
        MaxSingleDayProfit = 0m;
        CurrentDay = null;
        TradingDaysCount = 0;
        StartedAt = null;
        DrawdownWarned = false;
    }

    private void Fail(BreachReason reason)
    {
        Status = ChallengeStatus.Failed;
        Breach = reason;
        ReleaseAssignment();
        RaiseDomainEvent(new PropFirmChallengeBreached(Id, UserId, reason));
    }
}
