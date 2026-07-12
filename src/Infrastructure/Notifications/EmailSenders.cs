using System.Net;
using System.Net.Mail;
using Core.Logging;
using Core.Notifications;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

/// <summary>
/// Default sender for deployments with no mail transport: it never sends, only logs, and reports
/// <see cref="IsConfigured"/> = false so the registration flow downgrades email-verification to approval.
/// </summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.EmailNotConfigured(message.ToAddress, message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>
/// SMTP sender, active when <c>App:Email:Host</c> is configured. Wired only in that case (see the
/// Infrastructure DI registration), so a mis-set host can never silently swallow verification mail.
/// </summary>
public sealed class SmtpEmailSender(IOptionsMonitor<AppOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public bool IsConfigured => options.CurrentValue.Email.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var email = options.CurrentValue.Email;
        try
        {
            using var client = new SmtpClient(email.Host, email.Port)
            {
                EnableSsl = email.UseStartTls,
                Credentials = string.IsNullOrWhiteSpace(email.Username)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(email.Username, email.Password)
            };
            using var mail = new MailMessage
            {
                From = new MailAddress(email.FromAddress, email.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(message.ToAddress);
            await client.SendMailAsync(mail, ct);
            logger.EmailSent(message.ToAddress, message.Subject);
        }
        catch (Exception ex)
        {
            logger.EmailSendFailed(message.ToAddress, message.Subject, ex);
            throw;
        }
    }
}
