namespace Core.Calendar;

/// <summary>How much market impact a release historically carries, banded from the deterministic impact score.</summary>
public enum ImpactLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>How precisely a release instant is known when the event is scheduled.</summary>
public enum ReleasePrecision
{
    /// <summary>An exact published UTC instant (e.g. an FOMC decision at a stated time).</summary>
    Exact,

    /// <summary>Known to the calendar day only; the intraday time is not published.</summary>
    Day,

    /// <summary>A tentative / "during the week" window with no firm date.</summary>
    Tentative
}

/// <summary>The economic category a series belongs to — drives the impact prior and UI grouping.</summary>
public enum MarketMovingCategory
{
    InterestRate,
    Inflation,
    Employment,
    Growth,
    Sentiment,
    Trade,
    Housing,
    Consumption,
    Manufacturing,
    Other
}

/// <summary>How often a series prints, used to project the next release and validate schedules.</summary>
public enum ReleaseCadence
{
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    SemiAnnual,
    Annual,
    PerMeeting,
    Irregular
}

/// <summary>What a single append-only <c>EventRevision</c> records — the reason it was added.</summary>
public enum RevisionKind
{
    /// <summary>The event was first scheduled (no actual yet).</summary>
    Scheduled,

    /// <summary>The first printed actual arrived.</summary>
    Released,

    /// <summary>A later, revised actual/forecast/previous arrived.</summary>
    Revised,

    /// <summary>The source moved the release instant.</summary>
    Rescheduled,

    /// <summary>The impact score was recomputed under a new model version.</summary>
    Rescored
}
