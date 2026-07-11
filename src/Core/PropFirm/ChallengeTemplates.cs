namespace Core.PropFirm;

/// <summary>
/// Industry-standard challenge presets. Each builds a valid <see cref="ChallengeRules"/> for a
/// <see cref="ChallengeKind"/>; the UI pre-fills these and the user may adjust any field (the fully
/// custom path uses <see cref="ChallengeKind.Custom"/>). Presets model the common retail FX/CFD shapes:
/// one-phase (funded after a single evaluation, tighter limits), two-phase (Evaluation + Verification),
/// three-phase (smaller staged targets), and instant funding (no target-to-pass, strict drawdown).
/// </summary>
public static class ChallengeTemplates
{
    public static ChallengeRules For(ChallengeKind kind) => kind switch
    {
        ChallengeKind.OnePhase => new ChallengeRules(
            new Percent(10), new Percent(5), new Percent(6),
            DrawdownMode.Static, new TradingDayRequirement(0), SingleStep: true)
        {
            Kind = ChallengeKind.OnePhase,
            DailyLossBasis = DailyLossBasis.Equity
        },
        ChallengeKind.TwoPhase => new ChallengeRules(
            new Percent(8), new Percent(5), new Percent(10),
            DrawdownMode.Static, new TradingDayRequirement(3), SingleStep: false)
        {
            Kind = ChallengeKind.TwoPhase,
            DailyLossBasis = DailyLossBasis.Equity
        },
        ChallengeKind.ThreePhase => new ChallengeRules(
            new Percent(6), new Percent(5), new Percent(12),
            DrawdownMode.Static, new TradingDayRequirement(3), SingleStep: false)
        {
            Kind = ChallengeKind.ThreePhase,
            DailyLossBasis = DailyLossBasis.Equity
        },
        ChallengeKind.InstantFunding => new ChallengeRules(
            new Percent(10), new Percent(4), new Percent(6),
            DrawdownMode.Trailing, new TradingDayRequirement(0), SingleStep: true)
        {
            Kind = ChallengeKind.InstantFunding,
            DailyLossBasis = DailyLossBasis.Equity
        },
        _ => new ChallengeRules(
            new Percent(10), new Percent(5), new Percent(10),
            DrawdownMode.Static, new TradingDayRequirement(0), SingleStep: true)
        {
            Kind = ChallengeKind.Custom
        }
    };

    public static IReadOnlyList<ChallengeKind> All =>
    [
        ChallengeKind.OnePhase, ChallengeKind.TwoPhase, ChallengeKind.ThreePhase,
        ChallengeKind.InstantFunding, ChallengeKind.Custom
    ];
}
