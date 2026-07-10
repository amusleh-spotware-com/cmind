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

/// <summary>The pass/fail rule set of a prop-firm challenge phase.</summary>
public sealed record ChallengeRules(
    Percent ProfitTarget,
    Percent MaxDailyLoss,
    Percent MaxTotalDrawdown,
    DrawdownMode DrawdownMode,
    TradingDayRequirement MinTradingDays,
    bool SingleStep);
