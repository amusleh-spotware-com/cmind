using Core.Constants;

namespace Core.Domain;

public readonly record struct RiskPercent
{
    public double Value { get; }

    public RiskPercent(double value)
    {
        if (value is <= 0 or > 100) throw new DomainException(DomainErrors.RiskOutOfRange);
        Value = value;
    }

    public override string ToString() => Value.ToString("0.##");
}

public readonly record struct DrawdownPercent
{
    public double Value { get; }

    public DrawdownPercent(double value)
    {
        if (value is < 0 or > 100) throw new DomainException(DomainErrors.DrawdownOutOfRange);
        Value = value;
    }

    public override string ToString() => Value.ToString("0.##");
}

public readonly record struct EvaluationInterval
{
    public int Minutes { get; }

    public EvaluationInterval(int minutes)
    {
        if (minutes < AlertConstants.MinIntervalMinutes || minutes > AlertConstants.MaxIntervalMinutes)
            throw new DomainException(DomainErrors.IntervalOutOfRange);
        Minutes = minutes;
    }

    public override string ToString() => Minutes.ToString();
}

public readonly record struct AlertSeverity
{
    public static readonly AlertSeverity Info = new(AlertConstants.SeverityInfo);
    public static readonly AlertSeverity Warning = new(AlertConstants.SeverityWarning);
    public static readonly AlertSeverity Critical = new(AlertConstants.SeverityCritical);

    public string Value { get; }

    public AlertSeverity(string value)
    {
        var normalized = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.SeverityUnknown).ToLowerInvariant();
        if (normalized is not (AlertConstants.SeverityInfo or AlertConstants.SeverityWarning or AlertConstants.SeverityCritical))
            throw new DomainException(DomainErrors.SeverityUnknown);
        Value = normalized;
    }

    public override string ToString() => Value;
}
