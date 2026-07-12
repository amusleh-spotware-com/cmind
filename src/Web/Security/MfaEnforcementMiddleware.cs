using Core.Constants;
using Microsoft.AspNetCore.Http;

namespace Web.Security;

/// <summary>
/// White-label mandatory 2FA (App:Branding:RequireMfa). A user who has authenticated but still owes
/// enrollment carries the <see cref="MfaConstants.SetupRequiredClaimType"/> claim; this middleware redirects
/// their page navigations to <c>/account</c> until they finish setup. Only full-page GET (text/html) requests
/// are gated — API calls, the account page itself, auth routes, SignalR and static assets pass through so the
/// enrollment flow can complete.
/// </summary>
public sealed class MfaEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldRedirect(context))
        {
            context.Response.Redirect("/account");
            return;
        }
        await next(context);
    }

    private static bool ShouldRedirect(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true) return false;
        if (!user.HasClaim(MfaConstants.SetupRequiredClaimType, MfaConstants.SetupRequiredClaimValue)) return false;
        if (!HttpMethods.IsGet(context.Request.Method)) return false;

        var accept = context.Request.Headers.Accept.ToString();
        if (!accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)) return false;

        var path = context.Request.Path.Value ?? "/";
        return !IsAllowed(path);
    }

    private static bool IsAllowed(string path) =>
        path.StartsWith("/account", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/forbidden", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_", StringComparison.OrdinalIgnoreCase);
}
