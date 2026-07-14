using System.Security.Claims;
using Core;
using Core.Constants;
using Core.Localization;
using Core.Time;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Web.Security;

/// <summary>
/// Issues the authentication cookie for a signed-in user, embedding role, MFA-setup, must-change-password
/// and the user's <b>time-zone</b> claims, and seeds the culture + time-zone cookies from the user's profile
/// so the next request (and the Blazor circuit that boots from it) renders in the user's language and zone.
/// Shared by the login flow and the /set-timezone re-issue so both stay in lock-step.
/// </summary>
public static class AuthCookieIssuer
{
    public static async Task IssueAsync(HttpContext ctx, AppUser user, bool rememberMe,
        DateTimeOffset now, bool setupRequired)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleName)
        };
        if (TimeZoneId.TryFrom(user.Profile.TimeZone, out var zone))
            claims.Add(new Claim(TimeConstants.TimeZoneClaimType, zone.Value));
        if (setupRequired)
            claims.Add(new Claim(MfaConstants.SetupRequiredClaimType, MfaConstants.SetupRequiredClaimValue));
        if (user.MustChangePassword)
            claims.Add(new Claim(PasswordPolicyConstants.MustChangePasswordClaimType, PasswordPolicyConstants.MustChangePasswordClaimValue));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProps = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? now.AddDays(30) : null
        };
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), authProps);

        // Carry the user's saved language into the culture cookie so the very next request (and the Blazor
        // circuit that boots from it) renders in their language without waiting for a manual switch.
        if (CultureName.TryFrom(user.Profile.Locale, out var culture))
            AppendPersistentCookie(ctx, CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture.Value)));

        // Same for the time zone: seed the cookie the anonymous/SSR path reads, keeping it in sync with the claim.
        if (TimeZoneId.TryFrom(user.Profile.TimeZone, out var tz))
            AppendPersistentCookie(ctx, TimeConstants.TimeZoneCookieName, tz.Value);
    }

    private static void AppendPersistentCookie(HttpContext ctx, string name, string value) =>
        ctx.Response.Cookies.Append(name, value, new CookieOptions
        {
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            IsEssential = true,
            Path = "/"
        });
}
