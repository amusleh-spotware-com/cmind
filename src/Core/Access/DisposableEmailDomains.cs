namespace Core.Access;

/// <summary>
/// A small, curated block-list of well-known disposable / throwaway email providers. Not exhaustive by
/// design — it catches the common ones without a maintenance burden; a deployment that needs a fuller list
/// can front registration with its own CAPTCHA / allowed-domain policy.
/// </summary>
public static class DisposableEmailDomains
{
    private static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", "tempmail.com", "temp-mail.org",
        "throwawaymail.com", "yopmail.com", "getnada.com", "trashmail.com", "sharklasers.com",
        "dispostable.com", "maildrop.cc", "fakeinbox.com", "mailnesia.com", "mohmal.com",
        "emailondeck.com", "mintemail.com", "spam4.me", "tempinbox.com", "discard.email"
    };

    public static bool IsDisposable(string email)
    {
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return false;
        var domain = email[(at + 1)..].Trim();
        return Domains.Contains(domain);
    }
}
