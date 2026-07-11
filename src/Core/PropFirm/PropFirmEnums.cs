namespace Core.PropFirm;

public enum ChallengePhase
{
    Evaluation,
    Verification,
    Funded
}

public enum ChallengeStatus
{
    Active,
    Passed,
    Failed,
    Draft,
    Stopped
}

public enum DrawdownMode
{
    Static,
    Trailing,
    TrailingThreshold
}

public enum DailyLossBasis
{
    Equity,
    Balance
}

public enum ChallengeKind
{
    OnePhase,
    TwoPhase,
    ThreePhase,
    InstantFunding,
    Custom
}

public enum BreachReason
{
    None,
    DailyLoss,
    MaxDrawdown,
    Consistency,
    TimeLimit,
    Inactivity,
    WeekendHolding,
    NewsTrading,
    MaxExposure
}
