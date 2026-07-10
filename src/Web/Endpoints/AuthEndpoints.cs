using System.Security.Claims;
using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Endpoints;

public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicies.Auth);

        g.MapPost("/login", async (HttpContext ctx, DataContext db, IPasswordHasher hasher,
            TimeProvider timeProvider) =>
        {
            string email;
            string password;
            string? returnUrl;
            bool rememberMe;
            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync();
                email = form["Email"].ToString();
                password = form["Password"].ToString();
                returnUrl = form["ReturnUrl"].ToString();
                rememberMe = string.Equals(form["RememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var req = await ctx.Request.ReadFromJsonAsync<LoginRequest>();
                if (req is null) return Results.BadRequest();
                email = req.Email;
                password = req.Password;
                returnUrl = null;
                rememberMe = req.RememberMe;
            }

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
                if (ctx.Request.HasFormContentType)
                    return Results.Redirect("/login?error=1");
                return Results.Unauthorized();
            }
            user.RecordSuccessfulLogin();
            await db.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.RoleName)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProps = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? now.AddDays(30) : null
            };
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity), authProps);

            if (ctx.Request.HasFormContentType)
            {
                var target = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
                return Results.Redirect(target);
            }
            return Results.Ok(new { user.MustChangePassword });
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

        return app;
    }
}
