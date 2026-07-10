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

    public ChallengePhase Phase { get; private set; }
    public ChallengeStatus Status { get; private set; }
    public BreachReason Breach { get; private set; }

    public decimal CurrentEquity { get; private set; }
    public decimal PeakEquity { get; private set; }
    public decimal DailyStartEquity { get; private set; }
    public DateOnly? CurrentDay { get; private set; }
    public int TradingDaysCount { get; private set; }
    public DateTimeOffset? LastEquityAt { get; private set; }

    private PropFirmChallenge()
    {
    }

    public ChallengeRules Rules => new(
        new Percent(ProfitTargetPercent),
        new Percent(MaxDailyLossPercent),
        new Percent(MaxTotalDrawdownPercent),
        DrawdownMode,
        new TradingDayRequirement(MinTradingDays),
        SingleStep);

    public static PropFirmChallenge Create(UserId userId, TradingAccountId tradingAccountId, string name,
        Money startingBalance, ChallengeRules rules)
    {
        if (startingBalance.Amount <= 0) throw new DomainException(DomainErrors.PropFirmStartingBalanceInvalid);

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
            Phase = ChallengePhase.Evaluation,
            Status = ChallengeStatus.Active,
            Breach = BreachReason.None,
            CurrentEquity = startingBalance.Amount,
            PeakEquity = startingBalance.Amount,
            DailyStartEquity = startingBalance.Amount
        };

        challenge.RaiseDomainEvent(new PropFirmChallengeStarted(challenge.Id, userId));
        return challenge;
    }

    public void Rename(string name) =>
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);

    /// <summary>
    /// Feeds an equity snapshot and evaluates the rules: rolls the trading day, updates the peak, and either
    /// fails the challenge on a breach or advances the phase when the profit target and minimum-trading-day
    /// requirements are met. Only valid while the challenge is <see cref="ChallengeStatus.Active"/>.
    /// </summary>
    public void RecordEquity(Money equity, DateTimeOffset now)
    {
        if (Status != ChallengeStatus.Active) throw new DomainException(DomainErrors.PropFirmChallengeNotActive);
        if (LastEquityAt is { } last && now < last)
            throw new DomainException(DomainErrors.PropFirmEquityOutOfOrder);

        var day = DateOnly.FromDateTime(now.UtcDateTime);
        if (CurrentDay != day)
        {
            CurrentDay = day;
            DailyStartEquity = CurrentEquity;
            TradingDaysCount++;
        }

        CurrentEquity = equity.Amount;
        if (CurrentEquity > PeakEquity) PeakEquity = CurrentEquity;
        LastEquityAt = now;

        if (TryBreach()) return;
        TryPassPhase();
    }

    private bool TryBreach()
    {
        var dailyLossLimit = DailyStartEquity * (decimal)MaxDailyLossPercent / 100m;
        if (DailyStartEquity - CurrentEquity >= dailyLossLimit)
        {
            Fail(BreachReason.DailyLoss);
            return true;
        }

        var reference = DrawdownMode == DrawdownMode.Trailing ? PeakEquity : StartingBalance;
        var drawdownLimit = reference * (decimal)MaxTotalDrawdownPercent / 100m;
        if (reference - CurrentEquity >= drawdownLimit)
        {
            Fail(BreachReason.MaxDrawdown);
            return true;
        }

        return false;
    }

    private void TryPassPhase()
    {
        var target = StartingBalance * (decimal)ProfitTargetPercent / 100m;
        if (CurrentEquity - StartingBalance < target) return;
        if (TradingDaysCount < MinTradingDays) return;

        if (Phase == ChallengePhase.Evaluation && !SingleStep)
        {
            Phase = ChallengePhase.Verification;
            ResetBaseline();
            RaiseDomainEvent(new PropFirmPhasePassed(Id, UserId, Phase));
            return;
        }

        Phase = ChallengePhase.Funded;
        Status = ChallengeStatus.Passed;
        RaiseDomainEvent(new PropFirmPhasePassed(Id, UserId, Phase));
        RaiseDomainEvent(new PropFirmChallengePassed(Id, UserId));
    }

    private void ResetBaseline()
    {
        CurrentEquity = StartingBalance;
        PeakEquity = StartingBalance;
        DailyStartEquity = StartingBalance;
        CurrentDay = null;
        TradingDaysCount = 0;
    }

    private void Fail(BreachReason reason)
    {
        Status = ChallengeStatus.Failed;
        Breach = reason;
        RaiseDomainEvent(new PropFirmChallengeBreached(Id, UserId, reason));
    }
}
