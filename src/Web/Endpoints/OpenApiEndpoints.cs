using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using Infrastructure.OpenApi;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.OpenApi;

namespace Web.Endpoints;

public record SaveOpenApiAppRequest(string Name, string ClientId, string? ClientSecret);

public record SetOpenApiRateLimitRequest(string Category, int Value);

public static class OpenApiEndpoints
{
    private const string Scope = "trading";
    private static readonly TimeSpan AuthorizeStateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InviteTtl = TimeSpan.FromDays(14);

    public static IEndpointRouteBuilder MapOpenApiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/openapi").RequireAuthorization(AuthPolicies.UserOrAbove)
            .RequireFeature(Core.Features.FeatureFlag.OpenApi);

        g.MapGet("/application", async (IOpenApiAppResolver resolver, ICurrentUser u, DataContext db,
            IOptionsMonitor<AppOptions> options, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var sharedMode = await resolver.IsSharedModeAsync(ct);
            var application = await resolver.ResolveForUserAsync(uid, ct);
            var callbackUrl = RedirectUrl(options.CurrentValue.OpenApi, ctx);
            var authorizedAccountCount = await db.OpenApiAuthorizations.CountAsync(a => a.UserId == uid, ct);
            if (application is null)
                return Results.Ok(new { configured = false, sharedMode, callbackUrl, authorizedAccountCount });
            return Results.Ok(new
            {
                configured = true, sharedMode, application.Name, application.ClientId,
                callbackUrl, authorizedAccountCount
            });
        });

        g.MapPut("/application", async (SaveOpenApiAppRequest req, IOpenApiApplicationRepository apps,
            IOpenApiAppResolver resolver, ICurrentUser u, ISecretProtector p, IOptionsMonitor<AppOptions> options,
            HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (await resolver.IsSharedModeAsync(ct))
                return Results.Conflict(new { error = DomainErrors.OpenApiManagedByProvider });
            var redirectUri = new OpenApiRedirectUri(RedirectUrl(options.CurrentValue.OpenApi, ctx));
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

        g.MapDelete("/application", async (IOpenApiApplicationRepository apps, IOpenApiAppResolver resolver,
            ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (await resolver.IsSharedModeAsync(ct))
                return Results.Conflict(new { error = DomainErrors.OpenApiManagedByProvider });
            var application = await apps.GetByUserAsync(uid, ct);
            if (application is null) return Results.NotFound();
            await apps.RemoveAsync(application, ct);
            await apps.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/authorize", async (IOpenApiAppResolver resolver, ICurrentUser u,
            IOAuthStateService states, IOptionsMonitor<AppOptions> options, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = await resolver.ResolveForUserAsync(uid, ct);
            if (application is null) return Results.Redirect("/settings/openapi");
            var state = states.CreateState(uid, application.Id, AuthorizeStateTtl, isInvite: false);
            SetStateCookie(ctx, state);
            return Results.Redirect(BuildAuthorizeUrl(options.CurrentValue.OpenApi, application, state));
        });

        g.MapPost("/application/invite", async (IOpenApiAppResolver resolver, ICurrentUser u,
            IOAuthStateService states, HttpContext ctx, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = await resolver.ResolveForUserAsync(uid, ct);
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
                .FirstOrDefaultAsync(a => a.Id == result.ApplicationId && (a.IsShared || a.UserId == result.UserId));
            if (application is null) return Html(ErrorPage("The linked application no longer exists."));
            var authState = states.CreateState(result.UserId, result.ApplicationId, AuthorizeStateTtl, isInvite: true);
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
                .FirstOrDefaultAsync(a => a.Id == result.ApplicationId && (a.IsShared || a.UserId == result.UserId), ct);
            if (application is null) return Html(ErrorPage("The linked application no longer exists."));

            try
            {
                var clientSecret = Encoding.UTF8.GetString(
                    p.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
                var tokens = await tokenClient.ExchangeCodeAsync(
                    application.ClientId, clientSecret, code, application.RedirectUri, ct);
                var grant = await client.LoadGrantAsync(application.ClientId, clientSecret, tokens.AccessToken, ct);
                var linkResult = await linker.LinkAsync(result.UserId, application, grant, tokens, ct);
                var redirectTo = result.IsInvite
                    ? Core.Constants.OpenApiEndpoints.InviteRedirectPath
                    : Core.Constants.OpenApiEndpoints.AuthorizedRedirectPath;
                return Html(SuccessPage(linkResult.Linked, linkResult.SkippedBrokers, redirectTo));
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger(nameof(OpenApiEndpoints)).OpenApiCallbackFailed(ex);
                return Html(ErrorPage("Authorization failed. Please try again."));
            }
        }).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        var owner = app.MapGroup("/api/openapi/shared").RequireAuthorization(AuthPolicies.Owner)
            .RequireFeature(Core.Features.FeatureFlag.OpenApi);

        owner.MapGet("", async (SharedOpenApiAppService svc, DataContext db,
            IOptionsMonitor<AppOptions> options, HttpContext ctx, CancellationToken ct) =>
        {
            var shared = await svc.GetSharedAsync(ct);
            var redirectUrl = RedirectUrl(options.CurrentValue.OpenApi, ctx);
            if (shared is null)
                return Results.Ok(new { configured = false, redirectUrl, authorizedAccountCount = 0 });
            var authorizedAccountCount = await db.OpenApiAuthorizations.CountAsync(ct);
            return Results.Ok(new { configured = true, shared.Name, shared.ClientId, redirectUrl, authorizedAccountCount });
        });

        owner.MapPut("", async (SaveOpenApiAppRequest req, SharedOpenApiAppService svc,
            IOptionsMonitor<AppOptions> options, HttpContext ctx, CancellationToken ct) =>
        {
            var redirectUri = new OpenApiRedirectUri(RedirectUrl(options.CurrentValue.OpenApi, ctx));
            try
            {
                await svc.SaveOwnerSharedAsync(req.Name, new OpenApiClientId(req.ClientId), req.ClientSecret, redirectUri, ct);
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
            return Results.Ok();
        });

        owner.MapDelete("", async (SharedOpenApiAppService svc, CancellationToken ct) =>
            await svc.RemoveSharedAsync(ct) ? Results.NoContent() : Results.NotFound());

        owner.MapGet("/rate-limits", async (IOpenApiRateLimitProvider rates, CancellationToken ct) =>
            Results.Ok(await rates.GetEffectiveByNameAsync(ct)));

        owner.MapPut("/rate-limits", async (SetOpenApiRateLimitRequest req, IOpenApiRateLimitProvider rates,
            CancellationToken ct) =>
        {
            if (!Core.Constants.OpenApiSettings.TunableCategories.Contains(req.Category))
                return Results.BadRequest(new { error = "unknown_category" });
            if (req.Value < 0) return Results.BadRequest(new { error = "invalid_value" });
            await rates.SetOwnerOverrideAsync(req.Category, req.Value, ct);
            return Results.Ok();
        });

        return app;
    }

    private const string StateCookieName = Core.Constants.OpenApiEndpoints.StateCookieName;
    private const string CookiePath = Core.Constants.OpenApiEndpoints.CallbackPath;

    private static string CallbackUrl(HttpContext ctx) =>
        $"{ctx.Request.Scheme}://{ctx.Request.Host}{Core.Constants.OpenApiEndpoints.CallbackPath}";

    // The single canonical redirect URL every cTrader Open API app registers: composed from the configured
    // public base URL so it stays stable behind a proxy/CDN, falling back to the request host when unset.
    private static string RedirectUrl(Core.Options.OpenApiOptions options, HttpContext ctx) =>
        string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? CallbackUrl(ctx)
            : options.PublicBaseUrl.TrimEnd('/') + Core.Constants.OpenApiEndpoints.CallbackPath;

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

    private static string SuccessPage(int accountCount, IReadOnlyList<string> skippedBrokers, string redirectUrl)
    {
        var delay = Core.Constants.OpenApiEndpoints.SuccessRedirectDelaySeconds;
        var skippedNote = skippedBrokers.Count == 0
            ? string.Empty
            : $"<p class=\"muted\">{skippedBrokers.Count} account(s) were not added because their broker " +
              $"({System.Net.WebUtility.HtmlEncode(string.Join(", ", skippedBrokers.Distinct(StringComparer.OrdinalIgnoreCase)))}) " +
              "is not allowed on this deployment.</p>";
        return Page(
            "Accounts authorized",
            $"<h1>&#10003; You're all set</h1><p>{accountCount} trading account(s) were added and authorized.</p>" +
            skippedNote +
            $"<p class=\"muted\">Taking you back in <span id=\"redirect-countdown\">{delay}</span> seconds&hellip;</p>",
            redirectUrl);
    }

    private static string ErrorPage(string message) => Page(
        "Authorization problem",
        $"<h1>Something went wrong</h1><p>{System.Net.WebUtility.HtmlEncode(message)}</p>");

    private static string Page(string title, string body, string? redirectUrl = null)
    {
        var delay = Core.Constants.OpenApiEndpoints.SuccessRedirectDelaySeconds;
        var meta = redirectUrl is null
            ? string.Empty
            : $"<meta http-equiv=\"refresh\" content=\"{delay};url={System.Net.WebUtility.HtmlEncode(redirectUrl)}\">";
        // Tick the visible "N seconds" counter down each second, then redirect — so the countdown is live,
        // not a static number. The <meta refresh> above stays as a no-JS fallback.
        var script = redirectUrl is null
            ? string.Empty
            : "<script>(function(){var n=" + delay + ";var u=" + System.Text.Json.JsonSerializer.Serialize(redirectUrl) +
              ";var el=document.getElementById('redirect-countdown');var t=setInterval(function(){n--;" +
              "if(el)el.textContent=n<0?0:n;if(n<=0){clearInterval(t);location.href=u;}},1000);})();</script>";
        return $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{title}</title>{meta}" +
            "<style>body{font-family:system-ui,sans-serif;background:#1a1a1a;color:#eee;display:flex;" +
            "align-items:center;justify-content:center;height:100vh;margin:0}" +
            "div{max-width:32rem;padding:2rem;text-align:center}h1{color:#4caf50}.muted{color:#9e9e9e}</style></head>" +
            $"<body><div>{body}</div>{script}</body></html>";
    }
}
