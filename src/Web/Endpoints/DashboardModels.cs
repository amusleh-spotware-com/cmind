namespace Web.Endpoints;

/// <summary>Time window the dashboard aggregates over. Drives both the activity chart and KPI deltas.</summary>
public enum DashboardPeriod
{
    Hour,
    Day,
    Week,
    Month
}

/// <summary>Maps the wire period string to a window + bucket count. Pure — unit-tested.</summary>
public static class DashboardPeriods
{
    public static DashboardPeriod Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "1h" or "hour" => DashboardPeriod.Hour,
        "7d" or "week" => DashboardPeriod.Week,
        "30d" or "month" => DashboardPeriod.Month,
        _ => DashboardPeriod.Day
    };

    public static (TimeSpan Window, int Buckets) Plan(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Hour => (TimeSpan.FromHours(1), 12),
        DashboardPeriod.Week => (TimeSpan.FromDays(7), 7),
        DashboardPeriod.Month => (TimeSpan.FromDays(30), 30),
        _ => (TimeSpan.FromHours(24), 24)
    };
}

/// <summary>One instance reduced to the timestamps the dashboard buckets on. Created is always known;
/// Completed/Failed are the terminal-transition time when the instance reached that state.</summary>
public readonly record struct InstanceEvent(
    DateTimeOffset Created,
    DateTimeOffset? Completed,
    DateTimeOffset? Failed);

public sealed record DashboardBucket
{
    public required DateTimeOffset Start { get; init; }
    public required int Started { get; init; }
    public required int Completed { get; init; }
    public required int Failed { get; init; }
}

public sealed record DashboardKpis
{
    public required int ActiveNow { get; init; }
    public required int Completed { get; init; }
    public required int CompletedDelta { get; init; }
    public required int Failed { get; init; }
    public required int FailedDelta { get; init; }
    public required double SuccessRate { get; init; }
    public required double SuccessRateDelta { get; init; }
    public required IReadOnlyList<double> ActiveSpark { get; init; }
    public required IReadOnlyList<double> CompletedSpark { get; init; }
    public required IReadOnlyList<double> FailedSpark { get; init; }
    public required IReadOnlyList<double> SuccessSpark { get; init; }
}

public sealed record DashboardStatusBreakdown
{
    public required int Running { get; init; }
    public required int Pending { get; init; }
    public required int Failed { get; init; }
    public required int Completed { get; init; }
    public required int BacktestsRunning { get; init; }
    public required int Total { get; init; }
}

public sealed record DashboardActivity
{
    public required DateTimeOffset At { get; init; }
    public required string Kind { get; init; }
    public required string Status { get; init; }
    public string? Symbol { get; init; }
    public string? Timeframe { get; init; }
    public required string CBot { get; init; }
}

public sealed record DashboardResources
{
    public required int CBots { get; init; }
    public required int ParamSets { get; init; }
    public required int TradingAccounts { get; init; }
    public required int Ctids { get; init; }
    public required int McpKeys { get; init; }
}

public sealed record DashboardNodes
{
    public required int Total { get; init; }
    public required int Active { get; init; }
    public required int CapacityUsed { get; init; }
    public required int CapacityTotal { get; init; }
}

public sealed record DashboardOverview
{
    public required DateTimeOffset UpdatedAt { get; init; }
    public required bool IsAdmin { get; init; }
    public required DashboardKpis Kpis { get; init; }
    public required DashboardStatusBreakdown Status { get; init; }
    public required IReadOnlyList<DashboardBucket> TimeSeries { get; init; }
    public required IReadOnlyList<DashboardActivity> Activity { get; init; }
    public required DashboardResources Resources { get; init; }
    public DashboardNodes? Nodes { get; init; }
}

/// <summary>Pure aggregation: turns a set of <see cref="InstanceEvent"/> into time-bucketed series and
/// KPI cards with previous-period deltas. No I/O, no clock — the caller passes <c>now</c>, so this is
/// fully deterministic and unit-tested against hardcoded timestamps.</summary>
public static class DashboardMath
{
    public static IReadOnlyList<DashboardBucket> BuildBuckets(
        IReadOnlyCollection<InstanceEvent> events, DateTimeOffset now, TimeSpan window, int bucketCount)
    {
        var size = window / bucketCount;
        var origin = now - window;
        var started = new int[bucketCount];
        var completed = new int[bucketCount];
        var failed = new int[bucketCount];

        foreach (var e in events)
        {
            AddTo(started, e.Created, origin, size, bucketCount);
            if (e.Completed is { } c) AddTo(completed, c, origin, size, bucketCount);
            if (e.Failed is { } f) AddTo(failed, f, origin, size, bucketCount);
        }

        var list = new List<DashboardBucket>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            list.Add(new DashboardBucket
            {
                Start = origin + size * i,
                Started = started[i],
                Completed = completed[i],
                Failed = failed[i]
            });
        }

        return list;
    }

    public static DashboardKpis BuildKpis(
        IReadOnlyCollection<InstanceEvent> events, int activeNow, DateTimeOffset now, TimeSpan window, int bucketCount)
    {
        var buckets = BuildBuckets(events, now, window, bucketCount);
        var completedCurrent = buckets.Sum(b => b.Completed);
        var failedCurrent = buckets.Sum(b => b.Failed);

        var previousFrom = now - window * 2;
        var previousTo = now - window;
        var completedPrevious = 0;
        var failedPrevious = 0;
        foreach (var e in events)
        {
            if (e.Completed is { } c && c >= previousFrom && c < previousTo) completedPrevious++;
            if (e.Failed is { } f && f >= previousFrom && f < previousTo) failedPrevious++;
        }

        var successCurrent = SuccessRate(completedCurrent, failedCurrent);
        var successPrevious = SuccessRate(completedPrevious, failedPrevious);

        return new DashboardKpis
        {
            ActiveNow = activeNow,
            Completed = completedCurrent,
            CompletedDelta = completedCurrent - completedPrevious,
            Failed = failedCurrent,
            FailedDelta = failedCurrent - failedPrevious,
            SuccessRate = successCurrent,
            SuccessRateDelta = successCurrent - successPrevious,
            ActiveSpark = [.. buckets.Select(b => (double)b.Started)],
            CompletedSpark = [.. buckets.Select(b => (double)b.Completed)],
            FailedSpark = [.. buckets.Select(b => (double)b.Failed)],
            SuccessSpark = [.. buckets.Select(b => SuccessRate(b.Completed, b.Failed))]
        };
    }

    public static double SuccessRate(int completed, int failed)
    {
        var total = completed + failed;
        return total == 0 ? 0d : (double)completed / total;
    }

    private static void AddTo(int[] buckets, DateTimeOffset when, DateTimeOffset origin, TimeSpan size, int count)
    {
        if (when < origin) return;
        var index = (int)((when - origin).Ticks / size.Ticks);
        if (index == count) index = count - 1;
        if (index >= 0 && index < count) buckets[index]++;
    }
}
