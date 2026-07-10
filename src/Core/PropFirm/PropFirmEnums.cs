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
    Failed
}

public enum DrawdownMode
{
    Static,
    Trailing
}

public enum BreachReason
{
    None,
    DailyLoss,
    MaxDrawdown
}
