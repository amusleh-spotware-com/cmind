namespace Core.Options;

/// <summary>
/// How a self-registered account clears before it can sign in.
/// </summary>
public enum RegistrationMode
{
    /// <summary>Queue the account for an owner/admin to approve. Needs no mail infrastructure.</summary>
    AdminApproval,

    /// <summary>Send a single-use verification link; the account activates when the link is opened.</summary>
    EmailVerification,

    /// <summary>Activate immediately on submit — for trusted/dev deployments only.</summary>
    Open
}

/// <summary>
/// Per-attribute collection policy a white-label deployment sets for the registration form. Defaults to
/// <see cref="Off"/> so a stock deployment records only the email — the data-minimization baseline.
/// </summary>
public enum AttributePolicy
{
    Off,
    Optional,
    Required
}

/// <summary>
/// White-label control of self-service user registration, bound from <c>App:Registration</c>. Registration
/// is <b>disabled by default</b> — a stock/owner-managed deployment is unchanged. A reseller enables it and
/// tailors which user attributes are collected (and whether each is required) purely through configuration.
/// The collected-attribute set is deliberately minimal by default: the app does not custody funds, so KYC is
/// the broker's obligation, not this platform's — see the user-registration feature doc.
/// </summary>
public sealed record RegistrationOptions
{
    /// <summary>Master switch. When false, both the registration page and API return 404.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gating mode. When left at the default and no email sender is configured, the deployment falls back to
    /// <see cref="RegistrationMode.AdminApproval"/> at startup (see the registration options validator).
    /// </summary>
    public RegistrationMode Mode { get; init; } = RegistrationMode.AdminApproval;

    /// <summary>Role granted to a self-registered user. Never Owner/Admin — validated at startup.</summary>
    public string DefaultRole { get; init; } = "User";

    /// <summary>Require Terms/Privacy (+ Risk Disclosure) consent before the account is created.</summary>
    public bool RequireTermsAcceptance { get; init; } = true;

    /// <summary>Allow-list of email domains (e.g. <c>acme.com</c>). Empty = any domain accepted.</summary>
    public IReadOnlyList<string> AllowedEmailDomains { get; init; } = [];

    /// <summary>Reject well-known disposable/throwaway email providers.</summary>
    public bool BlockDisposableEmail { get; init; } = true;

    /// <summary>Lifetime of an email-verification token.</summary>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(24);

    public RegistrationCaptchaOptions Captcha { get; init; } = new();
    public RegistrationAttributeOptions Attributes { get; init; } = new();
    public RegistrationApiOptions Api { get; init; } = new();
}

/// <summary>
/// Optional CAPTCHA on the public form. Provider must expose the shared reCAPTCHA/hCaptcha/Turnstile verify
/// contract (POST <c>secret</c>+<c>response</c>, JSON <c>{ "success": bool }</c>).
/// </summary>
public sealed record RegistrationCaptchaOptions
{
    public bool Enabled { get; init; }
    public string VerifyUrl { get; init; } = string.Empty;
    public string SiteKey { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
}

/// <summary>
/// Which optional user attributes the registration form collects, and whether each is required. Every
/// attribute defaults to <see cref="AttributePolicy.Off"/>.
/// </summary>
public sealed record RegistrationAttributeOptions
{
    public AttributePolicy FullName { get; init; }
    public AttributePolicy DisplayName { get; init; }
    public AttributePolicy Country { get; init; }
    public AttributePolicy Phone { get; init; }
    public AttributePolicy Company { get; init; }
    public AttributePolicy Locale { get; init; }

    /// <summary>Marketing-email opt-in checkbox (unticked, separate from the mandatory consent).</summary>
    public AttributePolicy MarketingOptIn { get; init; }

    /// <summary>Age-confirmation checkbox (no date of birth is stored).</summary>
    public AttributePolicy AgeConfirmation { get; init; }
}

/// <summary>
/// Server-to-server provisioning endpoint for integrating another service. Off by default. The calling
/// service authenticates with <see cref="Secret"/> (a per-deployment provisioning key).
/// </summary>
public sealed record RegistrationApiOptions
{
    public bool Enabled { get; init; }

    /// <summary>Provisioning secret the caller presents in the <c>X-Provision-Secret</c> header.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Create the account already active (the caller vouches for the user).</summary>
    public bool ActivateImmediately { get; init; } = true;

    /// <summary>Force the provisioned user to change the (temporary) password on first sign-in.</summary>
    public bool InviteMustChangePassword { get; init; }
}

/// <summary>
/// Outbound email transport, bound from <c>App:Email</c>. When <see cref="Host"/> is unset the app uses a
/// no-op sender that only logs — so a deployment with no mail infrastructure runs unchanged and
/// email-verification registration transparently downgrades to admin approval.
/// </summary>
public sealed record EmailOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseStartTls { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
