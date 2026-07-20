using Core.Ai;
using Core.Constants;

namespace Core.Domain;

public enum AiRunStatus
{
    Running,
    Completed,
    Failed
}

/// <summary>
/// One persisted, backgroundable single-shot AI run — a Review or a Debate the user submits from its list
/// page. It captures the input (a title, language and source), runs the AI detached from the request (so it
/// survives navigation), and stores the model's output plus a status the UI polls. History: every past run
/// stays listed and viewable. Scoped to the owning user.
/// </summary>
public sealed class AiRun : AuditedEntity<AiRunId>
{
    private AiRun() { }

    public UserId UserId { get; private set; }

    public AiFeature Feature { get; private set; }

    public string Title { get; private set; } = default!;

    public string Language { get; private set; } = default!;

    public string Source { get; private set; } = default!;

    public AiRunStatus Status { get; private set; }

    public string? Output { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public bool IsTerminal => Status is AiRunStatus.Completed or AiRunStatus.Failed;

    public static AiRun Create(
        UserId userId, AiFeature feature, string title, string language, string source, DateTimeOffset now)
    {
        var run = new AiRun
        {
            UserId = userId,
            Feature = feature,
            Title = string.IsNullOrWhiteSpace(title) ? feature.ToString() : title.Trim(),
            Language = string.IsNullOrWhiteSpace(language) ? "CSharp" : language,
            Source = source ?? string.Empty,
            Status = AiRunStatus.Running
        };
        run.PreserveCreatedAt(now);
        return run;
    }

    public void Complete(string output, DateTimeOffset now)
    {
        if (IsTerminal) throw new DomainException(DomainErrors.AiRunAlreadyFinished);
        Status = AiRunStatus.Completed;
        Output = output;
        FinishedAt = now;
        PreserveUpdatedAt(now);
    }

    public void Fail(string error, DateTimeOffset now)
    {
        if (IsTerminal) throw new DomainException(DomainErrors.AiRunAlreadyFinished);
        Status = AiRunStatus.Failed;
        Error = error;
        FinishedAt = now;
        PreserveUpdatedAt(now);
    }
}
