using Core.Ai;
using Core.Constants;

namespace Core.Domain;

/// <summary>Lifecycle of an asynchronous AI task. Terminal = Succeeded/Failed/Cancelled.</summary>
public enum AiTaskStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>One appended log line on an <see cref="AiTask"/> — the durable tail of what the live stream shows.</summary>
public sealed record AiTaskLogEntry
{
    public int Sequence { get; init; }
    public DateTimeOffset Time { get; init; }
    public string Message { get; init; } = default!;
}

/// <summary>
/// A user-started AI operation that runs in the background so the user can navigate away and come back. It
/// captures the feature to run, the input payload, and the chosen provider credential (the selected model),
/// then moves Queued → Running → terminal as a worker claims and executes it on a self-healing lease (an
/// orphaned Running task whose lease expired is reclaimable). It records attempts, an appended log, the
/// output text, and references to anything it produced (e.g. a created cBot). All state changes go through
/// intention-revealing methods that guard the lifecycle; a terminal task rejects further transitions.
/// </summary>
public sealed class AiTask : AuditedEntity<AiTaskId>
{
    private readonly List<AiTaskLogEntry> _logs = [];

    private AiTask() { }

    public UserId UserId { get; private set; }
    public AiFeature Feature { get; private set; }
    public AiProviderCredentialId CredentialId { get; private set; }
    public AiTaskStatus Status { get; private set; }
    public string PayloadJson { get; private set; } = default!;
    public string? ResultText { get; private set; }
    public string? ResultRefsJson { get; private set; }
    public string? Error { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string? ClaimedBy { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public IReadOnlyList<AiTaskLogEntry> Logs => _logs;

    public bool IsTerminal => Status is AiTaskStatus.Succeeded or AiTaskStatus.Failed or AiTaskStatus.Cancelled;
    public bool IsActive => !IsTerminal;

    public static AiTask Create(
        UserId userId, AiFeature feature, AiProviderCredentialId credentialId, string payloadJson, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException(DomainErrors.AiTaskPromptRequired);

        var task = new AiTask
        {
            UserId = userId,
            Feature = feature,
            CredentialId = credentialId,
            Status = AiTaskStatus.Queued,
            PayloadJson = payloadJson
        };
        task.PreserveCreatedAt(now);
        return task;
    }

    /// <summary>True when this task can be claimed now: it is Queued, or Running with an expired lease (the
    /// owning node died) so another node may reclaim it.</summary>
    public bool IsClaimable(DateTimeOffset now) =>
        Status == AiTaskStatus.Queued
        || (Status == AiTaskStatus.Running && LeaseExpiresAt is { } expiry && expiry <= now);

    /// <summary>Claim the task for a worker and move it to Running under a fresh lease. Rejects a terminal
    /// task and a live-leased Running task (already owned by another node).</summary>
    public void Claim(string claimedBy, DateTimeOffset now, TimeSpan leaseTtl)
    {
        if (IsTerminal) throw new DomainException(DomainErrors.AiTaskAlreadyTerminal);
        if (!IsClaimable(now)) throw new DomainException(DomainErrors.AiTaskNotClaimable);

        Status = AiTaskStatus.Running;
        ClaimedBy = claimedBy;
        LeaseExpiresAt = now + leaseTtl;
        StartedAt ??= now;
        PreserveUpdatedAt(now);
    }

    /// <summary>Extend the lease while the task keeps running (called each work cycle by the owner).</summary>
    public void RenewLease(DateTimeOffset now, TimeSpan leaseTtl)
    {
        if (Status != AiTaskStatus.Running) throw new DomainException(DomainErrors.AiTaskNotClaimable);
        LeaseExpiresAt = now + leaseTtl;
        PreserveUpdatedAt(now);
    }

    public void RecordAttempt(DateTimeOffset now)
    {
        Attempts++;
        PreserveUpdatedAt(now);
    }

    /// <summary>Append a log line. A no-op once terminal so a late writer never mutates a finished task.</summary>
    public void Log(string message, DateTimeOffset now)
    {
        if (IsTerminal) return;
        _logs.Add(new AiTaskLogEntry { Sequence = _logs.Count, Time = now, Message = message });
        PreserveUpdatedAt(now);
    }

    public void Succeed(string resultText, string? resultRefsJson, DateTimeOffset now)
    {
        if (IsTerminal) throw new DomainException(DomainErrors.AiTaskAlreadyTerminal);
        Status = AiTaskStatus.Succeeded;
        ResultText = resultText;
        ResultRefsJson = resultRefsJson;
        FinishedAt = now;
        ClearLease();
        PreserveUpdatedAt(now);
    }

    public void Fail(string error, DateTimeOffset now)
    {
        if (IsTerminal) throw new DomainException(DomainErrors.AiTaskAlreadyTerminal);
        Status = AiTaskStatus.Failed;
        Error = error;
        FinishedAt = now;
        ClearLease();
        PreserveUpdatedAt(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        if (IsTerminal) throw new DomainException(DomainErrors.AiTaskAlreadyTerminal);
        Status = AiTaskStatus.Cancelled;
        FinishedAt = now;
        ClearLease();
        PreserveUpdatedAt(now);
    }

    private void ClearLease()
    {
        ClaimedBy = null;
        LeaseExpiresAt = null;
    }
}
