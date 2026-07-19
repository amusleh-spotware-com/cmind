namespace Core.Domain;

/// <summary>Author of a turn in an AI Build conversation.</summary>
public enum CBotBuildRole
{
    User,
    Assistant
}

/// <summary>
/// One persisted turn of the AI Build chat for a <see cref="CBotSourceProject"/>: the user's prompt or the
/// model's response, with its timestamp. Append-only — the whole conversation for a project is the ordered
/// list of these, so a user's prompts and the model's answers survive navigation and reload. Scoped to the
/// owning user; cascade-deleted with the project.
/// </summary>
public sealed class CBotBuildMessage : AuditedEntity<CBotBuildMessageId>
{
    private CBotBuildMessage() { }

    public CBotSourceProjectId ProjectId { get; private set; }

    public UserId UserId { get; private set; }

    public CBotBuildRole Role { get; private set; }

    public string Content { get; private set; } = default!;

    public static CBotBuildMessage Create(
        CBotSourceProjectId projectId, UserId userId, CBotBuildRole role, string content, DateTimeOffset now)
    {
        var message = new CBotBuildMessage
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            Content = content ?? string.Empty
        };
        message.PreserveCreatedAt(now);
        return message;
    }
}
