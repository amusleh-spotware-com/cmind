using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Core;
using Core.Constants;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Security;

namespace Web.Endpoints;

public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record VerifyTwoFactorRequest(string Code);
public record ConfirmMfaRequest(string Code);
public record PasswordConfirmRequest(string Password);

// Half-authenticated state carried in a short-lived, encrypted cookie between the password step and the
// TOTP challenge. Never an auth cookie; deleted the instant the challenge resolves.
internal sealed record MfaPendingTicket(string UserId, bool RememberMe, string? ReturnUrl, long IssuedAtUnix);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicies.Auth);

        g.MapPost("/login", async (HttpContext ctx, DataContext db, IPasswordHasher hasher,
            ISecretProtector protector, IOptionsMonitor<AppOptions> options, TimeProvider timeProvider) =>
        {
            var (email, password, returnUrl, rememberMe, isForm) = await ReadLoginAsync(ctx);

            var now = timeProvider.GetUtcNow();
            var normalized = email.ToUpperInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
            var lockedOut = user is not null && user.IsCurrentlyLockedOut(now);
            if (user is null || lockedOut || !hasher.Verify(password, user.PasswordHash))
            {
                if (user is not null && !lockedOut)
                {
                    user.RecordFailedLogin(AuthLockout.MaxFailedAttempts,
                        TimeSpan.FromMinutes(AuthLockout.LockoutMinutes), now);
                    await db.SaveChangesAsync();
                }
                return isForm ? Results.Redirect("/login?error=1") : Results.Unauthorized();
            }

            user.RecordSuccessfulLogin();
            await db.SaveChangesAsync();

            // Password OK. If the user already has 2FA, stop here and demand the second factor — do NOT
            // issue the auth cookie yet. Persist a signed pending ticket instead.
            if (user.MfaEnabled)
            {
                WritePendingTicket(ctx, protector, new MfaPendingTicket(
                    user.Id.Value.ToString(), rememberMe, NormalizeReturnUrl(returnUrl),
                    now.ToUnixTimeSeconds()));
                return isForm ? Results.Redirect("/login/2fa") : Results.Ok(new { MfaRequired = true });
            }

            var setupRequired = options.CurrentValue.Branding.RequireMfa && !user.MfaEnabled;
            await SignInAsync(ctx, user, rememberMe, now, setupRequired);

            if (isForm)
            {
                var target = setupRequired ? "/account" : NormalizeReturnUrl(returnUrl) ?? "/";
                return Results.Redirect(target);
            }
            return Results.Ok(new { user.MustChangePassword, MfaSetupRequired = setupRequired });
        }).DisableAntiforgery();

        g.MapPost("/login/verify-2fa", async (HttpContext ctx, DataContext db, ISecretProtector protector,
            ITotpAuthenticator totp, TimeProvider timeProvider) =>
        {
            var isForm = ctx.Request.HasFormContentType;
            var code = await ReadCodeAsync(ctx);
            var ticket = ReadPendingTicket(ctx, protector, timeProvider);
            if (ticket is null || !Guid.TryParse(ticket.UserId, out var userGuid))
            {
                DeletePendingTicket(ctx);
                return isForm ? Results.Redirect("/login?error=1") : Results.Unauthorized();
            }

            var now = timeProvider.GetUtcNow();
            var userId = UserId.From(userGuid);
            var user = await db.Users.Include(u => u.BackupCodes).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null || !user.MfaEnabled || user.IsCurrentlyLockedOut(now))
            {
                DeletePendingTicket(ctx);
                return isForm ? Results.Redirect("/login?error=1") : Results.Unauthorized();
            }

            var ok = VerifySecondFactor(user, code, protector, totp, now);
            if (!ok)
            {
                user.RecordFailedLogin(AuthLockout.MaxFailedAttempts,
                    TimeSpan.FromMinutes(AuthLockout.LockoutMinutes), now);
                await db.SaveChangesAsync();
                return isForm ? Results.Redirect("/login/2fa?error=1") : Results.Unauthorized();
            }

            user.RecordSuccessfulLogin();
            await db.SaveChangesAsync();
            DeletePendingTicket(ctx);
            await SignInAsync(ctx, user, ticket.RememberMe, now, setupRequired: false);

            return isForm ? Results.Redirect(ticket.ReturnUrl ?? "/") : Results.Ok();
        }).DisableAntiforgery();

        g.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        });

        g.MapPost("/change-password", async (ChangePasswordRequest req, ICurrentUser current,
            DataContext db, IPasswordHasher hasher) =>
        {
            if (current.UserId is not { } uid) return Results.Unauthorized();
            var user = await db.Users.FindAsync(uid);
            if (user is null) return Results.NotFound();
            if (!hasher.Verify(req.CurrentPassword, user.PasswordHash)) return Results.Unauthorized();
            user.ChangePassword(hasher.Hash(req.NewPassword));
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        MapMfaEndpoints(g);
        return app;
    }

    private static void MapMfaEndpoints(RouteGroupBuilder g)
    {
        var mfa = g.MapGroup("/mfa").RequireAuthorization();

        mfa.MapGet("/status", async (ICurrentUser current, DataContext db) =>
        {
            if (current.UserId is not { } uid) return Results.Unauthorized();
            var user = await db.Users.Include(u => u.BackupCodes).FirstOrDefaultAsync(u => u.Id == uid);
            if (user is null) return Results.NotFound();
            return Results.Ok(new
            {
                user.MfaEnabled,
                user.MfaEnrollmentPending,
                user.UnusedBackupCodeCount
            });
        });

        // Step 1: mint a fresh secret, store it (encrypted, not yet active) and return the QR + secret.
        mfa.MapPost("/setup", async (ICurrentUser current, DataContext db, ITotpAuthenticator totp,
            ISecretProtector protector, IOptionsMonitor<AppOptions> options) =>
        {
            if (current.UserId is not { } uid) return Results.Unauthorized();
            var user = await db.Users.FindAsync(uid);
            if (user is null) return Results.NotFound();
            if (user.MfaEnabled) return Results.Conflict(new { error = DomainErrors.MfaAlreadyEnabled });

            var secret = totp.GenerateSecret();
            var issuer = options.CurrentValue.Branding.ProductName;
            var uri = totp.BuildOtpAuthUri(issuer, user.Email, secret);
            user.BeginMfaEnrollment(protector.Protect(Encoding.UTF8.GetBytes(secret), EncryptionPurposes.MfaSecret));
            await db.SaveChangesAsync();

            return Results.Ok(new { Secret = secret, OtpAuthUri = uri, QrSvg = OtpQrCode.ToSvg(uri) });
        });

        // Step 2: verify a live code against the pending secret, then activate and hand back recovery codes.
        mfa.MapPost("/confirm", async (ConfirmMfaRequest req, HttpContext ctx, ICurrentUser current,
            DataContext db, ITotpAuthenticator totp, ISecretProtector protector,
            IOptionsMonitor<AppOptions> options, TimeProvider timeProvider) =>
        {
            if (current.UserId is not { } uid) return Results.Unauthorized();
            var user = await db.Users.Include(u => u.BackupCodes).FirstOrDefaultAsync(u => u.Id == uid);
            if (user is null) return Results.NotFound();
            if (!user.MfaEnrollmentPending || user.EncryptedMfaSecret is null)
                return Results.BadRequest(new { error = DomainErrors.MfaEnrollmentNotPending });

            var secret = Encoding.UTF8.GetString(
                protector.Unprotect(user.EncryptedMfaSecret, EncryptionPurposes.MfaSecret));
            if (!totp.VerifyCode(secret, req.Code, timeProvider.GetUtcNow()))
                return Results.BadRequest(new { error = "mfa.code_invalid" });

            var plaintextCodes = MfaBackupCodes.Generate(MfaConstants.BackupCodeCount, MfaConstants.BackupCodeLength);
            user.ConfirmMfaEnrollment([.. plaintextCodes.Select(MfaBackupCodes.Hash)]);
            await db.SaveChangesAsync();

            // Re-issue the auth cookie so the mandatory-setup claim (if any) is cleared now that MFA is on.
            var auth = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var persistent = auth.Properties?.IsPersistent ?? false;
            await SignInAsync(ctx, user, persistent, timeProvider.GetUtcNow(), setupRequired: false);

            return Results.Ok(new { BackupCodes = plaintextCodes.Select(MfaBackupCodes.Format).ToArray() });
        });

        mfa.MapPost("/disable", async (PasswordConfirmRequest req, ICurrentUser current, DataContext db,
            IPasswordHasher hasher) =>
        {
            if (current.UserId is not { } uid) return Results.Unauthorized();
            var user = await db.Users.Include(u => u.BackupCodes).FirstOrDefaultAsync(u => u.Id == uid);
            if (user is null) return Results.NotFound();
            if (!hasher.Verify(req.Password, user.PasswordHash)) return Results.Unauthorized();
            user.DisableMfa();
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        mfa.MapPost("/backup-codes/regenerate", async (PasswordConfirmRequest req, ICurrentUser current,
            DataContext db, IPasswordHasher hasher) =>
        {
            if (current.UserId is not { } uid) return Results.Unauthorized();
            var user = await db.Users.Include(u => u.BackupCodes).FirstOrDefaultAsync(u => u.Id == uid);
            if (user is null) return Results.NotFound();
            if (!user.MfaEnabled) return Results.BadRequest(new { error = DomainErrors.MfaNotEnabled });
            if (!hasher.Verify(req.Password, user.PasswordHash)) return Results.Unauthorized();

            var plaintextCodes = MfaBackupCodes.Generate(MfaConstants.BackupCodeCount, MfaConstants.BackupCodeLength);
            user.RegenerateBackupCodes([.. plaintextCodes.Select(MfaBackupCodes.Hash)]);
            await db.SaveChangesAsync();
            return Results.Ok(new { BackupCodes = plaintextCodes.Select(MfaBackupCodes.Format).ToArray() });
        });
    }

    private static bool VerifySecondFactor(AppUser user, string code, ISecretProtector protector,
        ITotpAuthenticator totp, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code) || user.EncryptedMfaSecret is null) return false;
        var secret = Encoding.UTF8.GetString(
            protector.Unprotect(user.EncryptedMfaSecret, EncryptionPurposes.MfaSecret));
        if (totp.VerifyCode(secret, code, now)) return true;
        // Fall back to a single-use recovery code.
        return user.ConsumeBackupCode(MfaBackupCodes.Hash(code), now);
    }

    private static async Task SignInAsync(HttpContext ctx, AppUser user, bool rememberMe,
        DateTimeOffset now, bool setupRequired)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleName)
        };
        if (setupRequired)
            claims.Add(new Claim(MfaConstants.SetupRequiredClaimType, MfaConstants.SetupRequiredClaimValue));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProps = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? now.AddDays(30) : null
        };
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), authProps);
    }

    private static async Task<(string Email, string Password, string? ReturnUrl, bool RememberMe, bool IsForm)>
        ReadLoginAsync(HttpContext ctx)
    {
        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            return (form["Email"].ToString(), form["Password"].ToString(), form["ReturnUrl"].ToString(),
                string.Equals(form["RememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase), true);
        }
        var req = await ctx.Request.ReadFromJsonAsync<LoginRequest>();
        return req is null ? ("", "", null, false, false) : (req.Email, req.Password, null, req.RememberMe, false);
    }

    private static async Task<string> ReadCodeAsync(HttpContext ctx)
    {
        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            return form["Code"].ToString();
        }
        var req = await ctx.Request.ReadFromJsonAsync<VerifyTwoFactorRequest>();
        return req?.Code ?? "";
    }

    private static string? NormalizeReturnUrl(string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') ? null : returnUrl;

    private static void WritePendingTicket(HttpContext ctx, ISecretProtector protector, MfaPendingTicket ticket)
    {
        var payload = protector.ProtectString(JsonSerializer.Serialize(ticket), EncryptionPurposes.MfaPendingCookie);
        ctx.Response.Cookies.Append(MfaConstants.PendingCookieName, payload, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(MfaConstants.PendingChallengeLifetimeMinutes),
            Path = "/"
        });
    }

    private static MfaPendingTicket? ReadPendingTicket(HttpContext ctx, ISecretProtector protector,
        TimeProvider timeProvider)
    {
        if (!ctx.Request.Cookies.TryGetValue(MfaConstants.PendingCookieName, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            var ticket = JsonSerializer.Deserialize<MfaPendingTicket>(
                protector.UnprotectString(raw, EncryptionPurposes.MfaPendingCookie));
            if (ticket is null) return null;
            var age = timeProvider.GetUtcNow().ToUnixTimeSeconds() - ticket.IssuedAtUnix;
            return age < MfaConstants.PendingChallengeLifetimeMinutes * 60 ? ticket : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void DeletePendingTicket(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(MfaConstants.PendingCookieName);
}
