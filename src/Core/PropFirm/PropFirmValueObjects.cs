using Core.Constants;
using Core.Domain;

namespace Core.PropFirm;

/// <summary>A non-negative account money amount (balance or equity).</summary>
public readonly record struct Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0) throw new DomainException(DomainErrors.PropFirmMoneyNegative);
        Amount = amount;
    }

    public static Money Zero => new(0m);
    public override string ToString() => Amount.ToString("0.##");
}

/// <summary>A percentage in the open-upper range (0, 100].</summary>
public readonly record struct Percent
{
    public double Value { get; }

    public Percent(double value)
    {
        if (value is <= 0 or > 100) throw new DomainException(DomainErrors.PropFirmPercentOutOfRange);
        Value = value;
    }

    public decimal Fraction => (decimal)Value / 100m;
    public override string ToString() => $"{Value:0.##}%";
}

/// <summary>A required number of distinct trading days (0..365).</summary>
public readonly record struct TradingDayRequirement
{
    public int Value { get; }

    public TradingDayRequirement(int value)
    {
        if (value is < 0 or > 365) throw new DomainException(DomainErrors.PropFirmTradingDaysOutOfRange);
        Value = value;
    }
}

/// <summary>A signed money delta (profit or loss) in the deposit currency.</summary>
public readonly record struct MoneyAmount
{
    public decimal Value { get; }

    public MoneyAmount(decimal value) => Value = value;

    public static MoneyAmount Zero => new(0m);
    public override string ToString() => Value.ToString("0.##");
}

/// <summary>A point-in-time account reading fed to the challenge: equity plus the realized balance.</summary>
public sealed record EquitySnapshot(Money Equity, Money Balance)
{
    public static EquitySnapshot FromEquity(Money equity) => new(equity, equity);
}

/// <summary>Non-equity trading facts the tracker observes, used to evaluate behaviour rules.</summary>
public sealed record ActivitySnapshot(
    int OpenPositions,
    bool OpenedInNewsWindow,
    bool HoldingOverWeekend);

/// <summary>The daily-loss constraint: a percent limit measured on either equity or balance.</summary>
public sealed record DailyLossLimit(Percent Limit, DailyLossBasis Basis)
{
    /// <summary>True when the day's loss has reached the limit for this basis.</summary>
    public bool IsBreached(decimal dailyStartEquity, decimal currentEquity,
        decimal dailyStartBalance, decimal currentBalance)
    {
        var (start, current) = Basis == DailyLossBasis.Balance
            ? (dailyStartBalance, currentBalance)
            : (dailyStartEquity, currentEquity);
        if (start <= 0) return false;
        return start - current >= start * Limit.Fraction;
    }
}

/// <summary>
/// The maximum-drawdown constraint. Static measures from the starting balance; Trailing from peak equity;
/// TrailingThreshold trails the equity peak by a fixed dollar amount until equity reaches a lock threshold,
/// after which the floor locks at the starting balance (futures-style).
/// </summary>
public sealed record DrawdownLimit(
    DrawdownMode Mode,
    Percent Percent,
    decimal TrailingThresholdAmount,
    decimal TrailingLockThreshold)
{
    public static DrawdownLimit Static(Percent percent) =>
        new(DrawdownMode.Static, percent, 0m, 0m);

    public static DrawdownLimit Trailing(Percent percent) =>
        new(DrawdownMode.Trailing, percent, 0m, 0m);

    public static DrawdownLimit TrailingThreshold(decimal trailAmount, decimal lockThreshold)
    {
        if (trailAmount <= 0) throw new DomainException(DomainErrors.PropFirmDrawdownThresholdInvalid);
        return new DrawdownLimit(DrawdownMode.TrailingThreshold, new Percent(100), trailAmount, lockThreshold);
    }

    public bool IsBreached(decimal startingBalance, decimal peakEquity, decimal currentEquity)
    {
        switch (Mode)
        {
            case DrawdownMode.Static:
                return startingBalance - currentEquity >= startingBalance * Percent.Fraction;
            case DrawdownMode.Trailing:
                return peakEquity - currentEquity >= peakEquity * Percent.Fraction;
            case DrawdownMode.TrailingThreshold:
                var locked = peakEquity >= TrailingLockThreshold;
                var floor = locked ? startingBalance : peakEquity - TrailingThresholdAmount;
                return currentEquity <= floor;
            default:
                return false;
        }
    }
}

/// <summary>The consistency constraint: no single day's profit may exceed a share of total profit.</summary>
public sealed record ConsistencyRule(Percent MaxSingleDayShare)
{
    public bool IsSatisfied(decimal maxSingleDayProfit, decimal totalProfit)
    {
        if (totalProfit <= 0) return true;
        return maxSingleDayProfit <= totalProfit * MaxSingleDayShare.Fraction;
    }
}

/// <summary>The pass/fail rule set of a prop-firm challenge phase.</summary>
public sealed record ChallengeRules(
    Percent ProfitTarget,
    Percent MaxDailyLoss,
    Percent MaxTotalDrawdown,
    DrawdownMode DrawdownMode,
    TradingDayRequirement MinTradingDays,
    bool SingleStep)
{
    public ChallengeKind Kind { get; init; } = ChallengeKind.Custom;
    public DailyLossBasis DailyLossBasis { get; init; } = DailyLossBasis.Equity;
    public decimal TrailingThresholdAmount { get; init; }
    public decimal TrailingLockThreshold { get; init; }
    public double? ConsistencyMaxDayProfitSharePercent { get; init; }
    public int? MaxCalendarDays { get; init; }
    public int? MaxInactivityDays { get; init; }
    public int? MaxOpenPositions { get; init; }
    public bool AllowWeekendHolding { get; init; } = true;
    public bool AllowNewsTrading { get; init; } = true;

    public DailyLossLimit DailyLoss() => new(MaxDailyLoss, DailyLossBasis);

    public DrawdownLimit Drawdown() => DrawdownMode switch
    {
        DrawdownMode.TrailingThreshold =>
            DrawdownLimit.TrailingThreshold(TrailingThresholdAmount, TrailingLockThreshold),
        DrawdownMode.Trailing => DrawdownLimit.Trailing(MaxTotalDrawdown),
        _ => DrawdownLimit.Static(MaxTotalDrawdown)
    };

    public ConsistencyRule? Consistency() => ConsistencyMaxDayProfitSharePercent is { } share
        ? new ConsistencyRule(new Percent(share))
        : null;
}
