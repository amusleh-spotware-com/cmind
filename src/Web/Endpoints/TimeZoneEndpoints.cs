using Core;
using Core.Constants;
using Core.Options;
using Core.Time;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Security;

namespace Web.Endpoints;

/// <summary>
/// The time-zone switch. A Blazor Server circuit can't read the request cookie or flip its zone live, so the
/// switcher (and the first-visit browser detector) navigate here (a plain HTTP GET, outside any circuit): we
/// validate the zone, write the time-zone cookie, persist it to the signed-in user's profile and re-issue the
/// auth cookie so the fresh circuit's <c>tz</c> claim is current, then redirect back with a full reload.
/// Mirrors <see cref="LocalizationEndpoints"/>.
/// </summary>
public static class TimeZoneEndpoints
{
    public static IEndpointRouteBuilder MapTimeZoneEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/set-timezone", async (string tz, string? redirectUri, bool? silent, HttpContext ctx,
            DataContext db, ICurrentUser current, IOptionsMonitor<AppOptions> options, TimeProvider time) =>
        {
            var isSilent = silent == true;

            // Only a zone the platform actually knows is honoured; anything else is ignored so a crafted query
            // can never push a garbage zone into the cookie or the profile.
            if (!TimeZoneId.TryFrom(tz, out var zone))
                return isSilent ? Results.NoContent() : Results.LocalRedirect(SafeRedirect(redirectUri));

            ctx.Response.Cookies.Append(TimeConstants.TimeZoneCookieName, zone.Value, new CookieOptions
            {
                MaxAge = TimeSpan.FromDays(365),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = ctx.Request.IsHttps,
                IsEssential = true,
                Path = "/"
            });

            if (current.UserId is { } uid)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == uid);
                if (user is not null)
                {
                    user.SetTimeZone(zone);
                    await db.SaveChangesAsync();

                    // Re-issue the auth cookie so the tz claim the next circuit reads reflects the new zone —
                    // only on the explicit switch (which force-reloads). The silent first-visit detector must
                    // NOT re-issue: changing the auth cookie under a LIVE circuit tears it down and reconnects.
                    if (!isSilent)
                    {
                        var auth = await ctx.AuthenticateAsync();
                        var persistent = auth.Properties?.IsPersistent == true;
                        var setupRequired = options.CurrentValue.Branding.RequireMfa && !user.MfaEnabled;
                        await AuthCookieIssuer.IssueAsync(ctx, user, persistent, time.GetUtcNow(), setupRequired);
                    }
                }
            }

            // The detector calls this in the background and wants no navigation; the switcher wants a redirect.
            return isSilent ? Results.NoContent() : Results.LocalRedirect(SafeRedirect(redirectUri));
        }).AllowAnonymous();

        return app;
    }

    // Only ever redirect back inside the app: a relative path starting with a single '/'. Anything else
    // (absolute URL, protocol-relative '//host') collapses to the dashboard, closing the open-redirect door.
    private static string SafeRedirect(string? redirectUri) =>
        !string.IsNullOrWhiteSpace(redirectUri)
        && redirectUri.StartsWith('/')
        && !redirectUri.StartsWith("//", StringComparison.Ordinal)
            ? redirectUri
            : "/";
}
