namespace Core.Notifications;

public sealed record EmailMessage(string ToAddress, string Subject, string HtmlBody, string? TextBody = null);

/// <summary>
/// Outbound transactional email. Implemented in Infrastructure. When no mail transport is configured the
/// registered implementation is a no-op that only logs, and <see cref="IsConfigured"/> is <c>false</c> — the
/// registration flow reads this to decide whether email-verification is actually deliverable.
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
