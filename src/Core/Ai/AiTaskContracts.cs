namespace Core.Ai;

/// <summary>Input payload for a "build a cBot" async task (feature <see cref="AiFeature.GenerateCBot"/>).</summary>
public sealed record AiTaskBuildPayload(string? Name, string? Language, string? Description);

/// <summary>Row in the user's AI-task list. Shows the human model name, never a raw credential id.</summary>
public sealed record AiTaskView(
    Guid Id,
    string Feature,
    string Status,
    string Model,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    string? Error);

/// <summary>Full detail of one AI task, including its output and appended log tail.</summary>
public sealed record AiTaskDetail(
    Guid Id,
    string Feature,
    string Status,
    string Model,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error,
    string? ResultText,
    string? ResultRefsJson,
    IReadOnlyList<AiTaskLogLine> Logs);

public sealed record AiTaskLogLine(int Sequence, DateTimeOffset Time, string Message);
