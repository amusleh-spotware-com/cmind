using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.OpenApi;

namespace Web.Endpoints;

public record SaveOpenApiAppRequest(string Name, string ClientId, string? ClientSecret);

public static class OpenApiEndpoints
{
    private const string Scope = "trading";
    private static readonly TimeSpan AuthorizeStateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InviteTtl = TimeSpan.FromDays(14);

    public static IEndpointRouteBuilder MapOpenApiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/openapi").RequireAuthorization(AuthPolicies.UserOrAbove);

        g.MapGet("/application", async (IOpenApiApplicationRepository apps, ICurrentUser u, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = await apps.GetByUserAsync(uid, ct);
            return application is null
                ? Results.Ok(new { configured = false, callbackUrl = CallbackUrl(ctx) })
                : Results.Ok(new { configured = true, application.Name, application.ClientId, callbackUrl = CallbackUrl(ctx) });
        });

        g.MapPut("/application", async (SaveOpenApiAppRequest req, IOpenApiApplicationRepository apps,
            ICurrentUser u, ISecretProtector p, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var redirectUri = new OpenApiRedirectUri(CallbackUrl(ctx));
            var existing = await apps.GetByUserAsync(uid, ct);
            if (existing is null)
            {
                if (string.IsNullOrEmpty(req.ClientSecret)) return Results.BadRequest(new { error = DomainErrors.OpenApiSecretRequired });
                var application = OpenApiApplication.Create(uid, req.Name, new OpenApiClientId(req.ClientId),
                    p.Protect(Encoding.UTF8.GetBytes(req.ClientSecret), EncryptionPurposes.OpenApiClientSecret), redirectUri);
                await apps.AddAsync(application, ct);
            }
            else
            {
                var secret = string.IsNullOrEmpty(req.ClientSecret)
                    ? existing.EncryptedClientSecret
                    : p.Protect(Encoding.UTF8.GetBytes(req.ClientSecret), EncryptionPurposes.OpenApiClientSecret);
                existing.UpdateCredentials(req.Name, new OpenApiClientId(req.ClientId), secret, redirectUri);
            }
            await apps.SaveChangesAsync(ct);
            return Results.Ok();
        });

        g.MapDelete("/application", async (IOpenApiApplicationRepository apps, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = await apps.GetByUserAsync(uid, ct);
            if (application is null) return Results.NotFound();
            await apps.RemoveAsync(application, ct);
            await apps.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/authorize", async (IOpenApiApplicationRepository apps, ICurrentUser u,
            IOAuthStateService states, IOptionsMonitor<AppOptions> options, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = await apps.GetByUserAsync(uid, ct);
            if (application is null) return Results.Redirect("/openapi-apps");
            var state = states.CreateState(uid, application.Id, AuthorizeStateTtl, isInvite: false);
            SetStateCookie(ctx, state);
            return Results.Redirect(BuildAuthorizeUrl(options.CurrentValue.OpenApi, application, state));
        });

        g.MapPost("/application/invite", async (IOpenApiApplicationRepository apps, ICurrentUser u,
            IOAuthStateService states, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = await apps.GetByUserAsync(uid, ct);
            if (application is null) return Results.NotFound();
            var state = states.CreateState(uid, application.Id, InviteTtl, isInvite: true);
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            return Results.Ok(new { url = $"{baseUrl}/openapi/invite/{Uri.EscapeDataString(state)}" });
        });

        app.MapGet("/openapi/invite/{state}", async (string state, DataContext db,
            IOAuthStateService states, IOptionsMonitor<AppOptions> options, HttpContext ctx) =>
        {
            var result = states.Validate(state);
            if (result is null || !result.IsInvite) return Html(ErrorPage("This invite link is invalid or expired."));
            var application = await db.OpenApiApplications
                .FirstOrDefaultAsync(a => a.Id == result.ApplicationId && a.UserId == result.UserId);
            if (application is null) return Html(ErrorPage("The linked application no longer exists."));
            var authState = states.CreateState(result.UserId, result.ApplicationId, AuthorizeStateTtl, isInvite: false);
            SetStateCookie(ctx, authState);
            return Results.Redirect(BuildAuthorizeUrl(options.CurrentValue.OpenApi, application, authState));
        }).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        app.MapGet("/openapi/callback", async (string? code, string? state, DataContext db,
            IOAuthStateService states, IOpenApiTokenClient tokenClient, IOpenApiClient client,
            OpenApiAccountLinker linker, ISecretProtector p, ILoggerFactory loggerFactory, HttpContext ctx,
            CancellationToken ct) =>
        {
            var effectiveState = state;
            if (string.IsNullOrEmpty(effectiveState) && ctx.Request.Cookies.TryGetValue(StateCookieName, out var cookieState))
                effectiveState = cookieState;
            ClearStateCookie(ctx);

            if (string.IsNullOrEmpty(code)) return Html(ErrorPage("Missing authorization code."));
            if (string.IsNullOrEmpty(effectiveState)) return Html(ErrorPage("Missing authorization state. Please start again."));

            var result = states.Validate(effectiveState);
            if (result is null) return Html(ErrorPage("This authorization link is invalid or expired."));

            var application = await db.OpenApiApplications
                .FirstOrDefaultAsync(a => a.Id == result.ApplicationId && a.UserId == result.UserId, ct);
            if (application is null) return Html(ErrorPage("The linked application no longer exists."));

            try
            {
                var clientSecret = Encoding.UTF8.GetString(
                    p.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
                var tokens = await tokenClient.ExchangeCodeAsync(
                    application.ClientId, clientSecret, code, application.RedirectUri, ct);
                var grant = await client.LoadGrantAsync(application.ClientId, clientSecret, tokens.AccessToken, ct);
                await linker.LinkAsync(result.UserId, application, grant, tokens, ct);
                return Html(SuccessPage(grant.Accounts.Count));
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger(nameof(OpenApiEndpoints)).OpenApiCallbackFailed(ex);
                return Html(ErrorPage("Authorization failed. Please try again."));
            }
        }).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        return app;
    }

    private const string StateCookieName = Core.Constants.OpenApiEndpoints.StateCookieName;
    private const string CookiePath = Core.Constants.OpenApiEndpoints.CallbackPath;

    private static string CallbackUrl(HttpContext ctx) =>
        $"{ctx.Request.Scheme}://{ctx.Request.Host}{Core.Constants.OpenApiEndpoints.CallbackPath}";

    private static void SetStateCookie(HttpContext ctx, string state) =>
        ctx.Response.Cookies.Append(StateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            MaxAge = AuthorizeStateTtl,
        });

    private static void ClearStateCookie(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(StateCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
        });

    private static string BuildAuthorizeUrl(OpenApiOptions options, OpenApiApplication application, string state)
    {
        var query =
            $"client_id={Uri.EscapeDataString(application.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(application.RedirectUri)}" +
            $"&scope={Scope}" +
            $"&state={Uri.EscapeDataString(state)}";
        return $"{options.AuthBaseUrl}{Core.Constants.OpenApiEndpoints.AuthorizePath}?{query}";
    }

    private static IResult Html(string html) => Results.Content(html, "text/html; charset=utf-8");

    private static string SuccessPage(int accountCount) => Page(
        "Accounts authorized",
        $"<h1>&#10003; You're all set</h1><p>{accountCount} trading account(s) were added and authorized. " +
        "You can close this window.</p>");

    private static string ErrorPage(string message) => Page(
        "Authorization problem",
        $"<h1>Something went wrong</h1><p>{System.Net.WebUtility.HtmlEncode(message)}</p>");

    private static string Page(string title, string body) =>
        $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{title}</title>" +
        "<style>body{font-family:system-ui,sans-serif;background:#1a1a1a;color:#eee;display:flex;" +
        "align-items:center;justify-content:center;height:100vh;margin:0}" +
        "div{max-width:32rem;padding:2rem;text-align:center}h1{color:#4caf50}</style></head>" +
        $"<body><div>{body}</div></body></html>";
}
