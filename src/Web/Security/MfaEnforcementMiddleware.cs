using Core.Constants;
using Microsoft.AspNetCore.Http;

namespace Web.Security;

/// <summary>
/// Gates page navigations for a fully-authenticated principal that still owes an account action, redirecting
/// to <c>/account</c> until it is done. Two triggers: white-label mandatory 2FA (App:Branding:RequireMfa,
/// <see cref="MfaConstants.SetupRequiredClaimType"/>) and a temporary/reset password
/// (<see cref="PasswordPolicyConstants.MustChangePasswordClaimType"/>) — the latter stops a temp-password
/// session from roaming the app before a new password is set. Only full-page GET (text/html) requests are
/// gated — API calls, the account page itself, auth routes, SignalR and static assets pass through so the
/// remediation flow can complete.
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
        var owesAction =
            user.HasClaim(MfaConstants.SetupRequiredClaimType, MfaConstants.SetupRequiredClaimValue)
            || user.HasClaim(PasswordPolicyConstants.MustChangePasswordClaimType, PasswordPolicyConstants.MustChangePasswordClaimValue);
        if (!owesAction) return false;
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
