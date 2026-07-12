using System.Security.Cryptography;
using System.Text;
using Core;
using Core.Access;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Notifications;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Endpoints;

public record RegisterRequest(
    string Email, string Password, string? FullName = null, string? DisplayName = null,
    string? Country = null, string? Phone = null, string? Company = null, string? Locale = null,
    bool MarketingOptIn = false, bool AgeConfirmed = false, bool AcceptTerms = false,
    string? CaptchaToken = null);

public record ResendVerificationRequest(string Email);

public record ProvisionUserRequest(
    string Email, string Password, int Role = 2, string? FullName = null, string? Country = null,
    string? Company = null);

public static class RegistrationEndpoints
{
    private const int MinPasswordLength = 8;

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/register")
            .RequireRateLimiting(RateLimitPolicies.Registration)
            .RequireFeature(Core.Features.FeatureFlag.Registration)
            .AddEndpointFilter(async (ctx, next) =>
            {
                var options = ctx.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppOptions>>();
                return options.CurrentValue.Registration.Enabled ? await next(ctx) : Results.NotFound();
            });

        g.MapGet("/config", (IOptionsMonitor<AppOptions> options, IEmailSender email) =>
        {
            var reg = options.CurrentValue.Registration;
            var a = reg.Attributes;
            return Results.Ok(new
            {
                enabled = reg.Enabled,
                mode = EffectiveMode(reg, email).ToString(),
                requireTerms = reg.RequireTermsAcceptance,
                allowedEmailDomains = reg.AllowedEmailDomains,
                captcha = new { enabled = reg.Captcha.Enabled, siteKey = reg.Captcha.SiteKey },
                attributes = new
                {
                    fullName = a.FullName.ToString(),
                    displayName = a.DisplayName.ToString(),
                    country = a.Country.ToString(),
                    phone = a.Phone.ToString(),
                    company = a.Company.ToString(),
                    locale = a.Locale.ToString(),
                    marketingOptIn = a.MarketingOptIn.ToString(),
                    ageConfirmation = a.AgeConfirmation.ToString()
                }
            });
        });

        g.MapPost("/", async (RegisterRequest req, HttpContext ctx, DataContext db,
            ILegalDocumentRepository docs, IConsentRepository consents, IPasswordHasher hasher,
            ICaptchaValidator captcha, IEmailSender email, IOptionsMonitor<AppOptions> options,
            TimeProvider clock, ILogger<Program> logger, CancellationToken ct) =>
        {
            var reg = options.CurrentValue.Registration;
            var ip = ctx.Connection.RemoteIpAddress?.ToString();

            if (!await captcha.ValidateAsync(req.CaptchaToken, ip, ct))
                return Results.BadRequest(new { error = "registration.captcha_failed" });

            if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@', StringComparison.Ordinal))
                return Results.BadRequest(new { error = "registration.email_invalid" });
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < MinPasswordLength)
                return Results.BadRequest(new { error = "registration.password_weak" });
            if (!IsEmailDomainAllowed(req.Email, reg))
                return Results.BadRequest(new { error = "registration.email_not_allowed" });
            if (reg.BlockDisposableEmail && DisposableEmailDomains.IsDisposable(req.Email))
                return Results.BadRequest(new { error = "registration.email_not_allowed" });
            if (reg.RequireTermsAcceptance && !req.AcceptTerms)
                return Results.BadRequest(new { error = "registration.terms_required" });

            var attributeError = ValidateRequiredAttributes(req, reg.Attributes);
            if (attributeError is not null) return Results.BadRequest(new { error = attributeError });

            UserProfile profile;
            Email email0;
            try
            {
                email0 = new Email(req.Email);
                profile = UserProfile.Create(req.FullName, req.DisplayName, req.Country, req.Phone,
                    req.Company, req.Locale, req.MarketingOptIn, req.AgeConfirmed);
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "registration.email_invalid" });
            }

            // Anti-enumeration: a duplicate email yields the SAME neutral response as a fresh signup, and
            // creates nothing. We never disclose whether an address already has an account.
            var normalized = email0.Normalized;
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
                return NeutralAccepted(EffectiveMode(reg, email));

            var mode = EffectiveMode(reg, email);
            var initialState = mode switch
            {
                RegistrationMode.EmailVerification => UserActivationState.PendingEmailVerification,
                RegistrationMode.AdminApproval => UserActivationState.PendingApproval,
                _ => UserActivationState.Active
            };

            var roleRank = RoleRank(reg.DefaultRole);
            AppUser user;
            try
            {
                user = AppUser.SelfRegister(roleRank, email0, hasher.Hash(req.Password),
                    RandomNumberGenerator.GetBytes(32), profile, initialState);
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            logger.UserSelfRegistered(user.Id.Value, initialState.ToString());

            if (reg.RequireTermsAcceptance)
                await RecordConsentAsync(user.Id, docs, consents, ip, clock.GetUtcNow(), ct);

            if (mode == RegistrationMode.EmailVerification)
                await IssueAndSendVerificationAsync(user, req.Email, ctx, db, email, reg, options, clock, ct);

            return NeutralAccepted(mode);
        }).DisableAntiforgery();

        g.MapGet("/verify", async (string? token, DataContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token)) return Results.Redirect("/login?verifyError=1");
            var hash = RegistrationTokens.Hash(token);
            var user = await db.Users.Include(u => u.EmailVerificationTokens)
                .FirstOrDefaultAsync(u => u.EmailVerificationTokens.Any(t => t.TokenHash == hash), ct);
            if (user is null || !user.RedeemEmailVerificationToken(hash, clock.GetUtcNow()))
                return Results.Redirect("/login?verifyError=1");
            await db.SaveChangesAsync(ct);
            return Results.Redirect("/login?verified=1");
        });

        g.MapPost("/resend", async (ResendVerificationRequest req, HttpContext ctx, DataContext db,
            IEmailSender email, IOptionsMonitor<AppOptions> options, TimeProvider clock, CancellationToken ct) =>
        {
            var reg = options.CurrentValue.Registration;
            if (EffectiveMode(reg, email) == RegistrationMode.EmailVerification
                && !string.IsNullOrWhiteSpace(req.Email))
            {
                var normalized = req.Email.ToUpperInvariant();
                var user = await db.Users.Include(u => u.EmailVerificationTokens)
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
                if (user is { ActivationState: UserActivationState.PendingEmailVerification })
                    await IssueAndSendVerificationAsync(user, req.Email, ctx, db, email, reg, options, clock, ct);
            }
            // Neutral: never reveal whether the address exists / is pending.
            return Results.Accepted();
        }).DisableAntiforgery();

        MapProvisioningEndpoint(app);
        return app;
    }

    private static void MapProvisioningEndpoint(IEndpointRouteBuilder app)
    {
        // Server-to-server user provisioning for an integrating service. Gated by the master Registration
        // feature flag AND its own Api.Enabled switch, authenticated by a per-deployment provisioning secret.
        app.MapPost("/api/provision", async (ProvisionUserRequest req, HttpContext ctx, DataContext db,
                IPasswordHasher hasher, IOptionsMonitor<AppOptions> options, ILogger<Program> logger,
                CancellationToken ct) =>
            {
                var api = options.CurrentValue.Registration.Api;
                if (!api.Enabled) return Results.NotFound();

                var presented = ctx.Request.Headers[RegistrationConstants.ProvisionSecretHeader].ToString();
                if (!ConstantTimeEquals(presented, api.Secret)) return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@', StringComparison.Ordinal))
                    return Results.BadRequest(new { error = "registration.email_invalid" });
                if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < MinPasswordLength)
                    return Results.BadRequest(new { error = "registration.password_weak" });
                if (req.Role is not (RegistrationConstants.RoleRankUser or RegistrationConstants.RoleRankViewer))
                    return Results.BadRequest(new { error = DomainErrors.RegistrationRoleNotAllowed });

                var normalized = req.Email.ToUpperInvariant();
                if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
                    return Results.Conflict(new { error = "registration.email_exists" });

                UserProfile profile;
                Email email0;
                try
                {
                    email0 = new Email(req.Email);
                    profile = UserProfile.Create(fullName: req.FullName, countryCode: req.Country, company: req.Company);
                }
                catch (DomainException ex) { return Results.BadRequest(new { error = ex.Code }); }
                catch (ArgumentException) { return Results.BadRequest(new { error = "registration.email_invalid" }); }

                var state = api.ActivateImmediately
                    ? UserActivationState.Active
                    : UserActivationState.PendingApproval;
                AppUser user;
                try
                {
                    user = AppUser.SelfRegister(req.Role, email0, hasher.Hash(req.Password),
                        RandomNumberGenerator.GetBytes(32), profile, state, api.InviteMustChangePassword);
                }
                catch (DomainException ex) { return Results.BadRequest(new { error = ex.Code }); }

                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
                logger.UserProvisioned(user.Id.Value);
                return Results.Ok(new { user.Id });
            })
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .RequireFeature(Core.Features.FeatureFlag.Registration)
            .DisableAntiforgery();
    }

    private static async Task IssueAndSendVerificationAsync(AppUser user, string toAddress, HttpContext ctx,
        DataContext db, IEmailSender email, RegistrationOptions reg, IOptionsMonitor<AppOptions> options,
        TimeProvider clock, CancellationToken ct)
    {
        var raw = RegistrationTokens.Generate();
        user.IssueEmailVerificationToken(RegistrationTokens.Hash(raw), clock.GetUtcNow().Add(reg.TokenLifetime));
        await db.SaveChangesAsync(ct);

        var product = options.CurrentValue.Branding.ProductName;
        var link = $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/register/verify?token={Uri.EscapeDataString(raw)}";
        var html = $"""
            <p>Welcome to {product}.</p>
            <p>Confirm your email address to activate your account:</p>
            <p><a href="{link}">Verify my email</a></p>
            <p>This link expires in {reg.TokenLifetime.TotalHours:0} hours. If you did not request this, ignore this email.</p>
            """;
        await email.SendAsync(new EmailMessage(toAddress, $"Verify your {product} account", html), ct);
    }

    private static async Task RecordConsentAsync(UserId userId, ILegalDocumentRepository docs,
        IConsentRepository consents, string? ip, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var doc in await docs.ListActiveAsync(ct))
            await consents.AddAsync(ConsentRecord.Accept(userId, doc.Type, doc.Version, now, ip), ct);
        await consents.SaveChangesAsync(ct);
    }

    private static RegistrationMode EffectiveMode(RegistrationOptions reg, IEmailSender email) =>
        reg.Mode == RegistrationMode.EmailVerification && !email.IsConfigured
            ? RegistrationMode.AdminApproval
            : reg.Mode;

    private static IResult NeutralAccepted(RegistrationMode mode) =>
        Results.Accepted(value: new
        {
            status = mode == RegistrationMode.EmailVerification ? "pending_email_verification"
                : mode == RegistrationMode.AdminApproval ? "pending_approval" : "active"
        });

    private static bool IsEmailDomainAllowed(string emailAddress, RegistrationOptions reg)
    {
        if (reg.AllowedEmailDomains.Count == 0) return true;
        var at = emailAddress.LastIndexOf('@');
        if (at < 0 || at == emailAddress.Length - 1) return false;
        var domain = emailAddress[(at + 1)..].Trim();
        return reg.AllowedEmailDomains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ValidateRequiredAttributes(RegisterRequest req, RegistrationAttributeOptions a)
    {
        if (a.FullName == AttributePolicy.Required && string.IsNullOrWhiteSpace(req.FullName))
            return "registration.fullname_required";
        if (a.DisplayName == AttributePolicy.Required && string.IsNullOrWhiteSpace(req.DisplayName))
            return "registration.displayname_required";
        if (a.Country == AttributePolicy.Required && string.IsNullOrWhiteSpace(req.Country))
            return "registration.country_required";
        if (a.Phone == AttributePolicy.Required && string.IsNullOrWhiteSpace(req.Phone))
            return "registration.phone_required";
        if (a.Company == AttributePolicy.Required && string.IsNullOrWhiteSpace(req.Company))
            return "registration.company_required";
        if (a.Locale == AttributePolicy.Required && string.IsNullOrWhiteSpace(req.Locale))
            return "registration.locale_required";
        if (a.AgeConfirmation == AttributePolicy.Required && !req.AgeConfirmed)
            return "registration.age_confirmation_required";
        return null;
    }

    private static int RoleRank(string? defaultRole) =>
        string.Equals(defaultRole?.Trim(), "Viewer", StringComparison.OrdinalIgnoreCase)
            ? RegistrationConstants.RoleRankViewer
            : RegistrationConstants.RoleRankUser;

    private static bool ConstantTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ab.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
