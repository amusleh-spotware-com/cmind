using Core;
using Core.Localization;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Web.Security;

namespace Web.Endpoints;

/// <summary>
/// The culture switch. A Blazor Server circuit captures its UI culture when it starts and cannot flip it
/// live, so the language switcher navigates here (a plain HTTP GET, outside any circuit): we write the
/// culture cookie, persist the choice to the signed-in user's profile, and redirect back with a full
/// reload — the new circuit then boots in the chosen language and re-evaluates layout direction (RTL).
/// </summary>
public static class LocalizationEndpoints
{
    public static IEndpointRouteBuilder MapLocalizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/set-culture", async (string culture, string? redirectUri,
            HttpContext ctx, DataContext db, ICurrentUser current) =>
        {
            // Only a culture the app actually localizes into is honoured; anything else is ignored so a
            // crafted query can never push an unsupported/garbage culture into the cookie or the profile.
            if (!CultureName.TryFrom(culture, out var cultureName))
                return Results.LocalRedirect(SafeRedirect(redirectUri));

            ctx.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureName.Value)),
                new CookieOptions
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
                    user.SetLocale(cultureName);
                    await db.SaveChangesAsync();
                }
            }

            return Results.LocalRedirect(SafeRedirect(redirectUri));
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
