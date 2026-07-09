using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Web.OpenApi;

namespace Web.Endpoints;

public record CreateOpenApiAppRequest(string Name, string ClientId, string ClientSecret, string RedirectUri);
public record UpdateOpenApiAppRequest(string Name, string ClientId, string? ClientSecret, string RedirectUri);

public static class OpenApiEndpoints
{
    private const string Scope = "trading";
    private static readonly TimeSpan AuthorizeStateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InviteTtl = TimeSpan.FromDays(14);

    public static IEndpointRouteBuilder MapOpenApiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/openapi").RequireAuthorization(AuthPolicies.UserOrAbove);

        g.MapGet("/applications", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.OpenApiApplications.Where(a => a.UserId == uid)
                .Select(a => new { a.Id, a.Name, a.ClientId, a.RedirectUri }).ToListAsync();
        });

        g.MapPost("/applications", async (CreateOpenApiAppRequest req, DataContext db, ICurrentUser u, ISecretProtector p) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var application = OpenApiApplication.Create(uid, req.Name, new OpenApiClientId(req.ClientId),
                p.Protect(Encoding.UTF8.GetBytes(req.ClientSecret), EncryptionPurposes.OpenApiClientSecret),
                new OpenApiRedirectUri(req.RedirectUri));
            db.OpenApiApplications.Add(application);
            await db.SaveChangesAsync();
            return Results.Ok(new { application.Id });
        });

        g.MapPut("/applications/{id:guid}", async (Guid id, UpdateOpenApiAppRequest req,
            DataContext db, ICurrentUser u, ISecretProtector p) =>
        {
            var uid = u.UserId!.Value;
            var aid = OpenApiApplicationId.From(id);
            var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == aid && a.UserId == uid);
            if (application is null) return Results.NotFound();
            var secret = string.IsNullOrEmpty(req.ClientSecret)
                ? application.EncryptedClientSecret
                : p.Protect(Encoding.UTF8.GetBytes(req.ClientSecret), EncryptionPurposes.OpenApiClientSecret);
            application.UpdateCredentials(req.Name, new OpenApiClientId(req.ClientId), secret,
                new OpenApiRedirectUri(req.RedirectUri));
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/applications/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var aid = OpenApiApplicationId.From(id);
            var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == aid && a.UserId == uid);
            if (application is null) return Results.NotFound();
            db.OpenApiApplications.Remove(application);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/applications/{id:guid}/authorize-url", async (Guid id, DataContext db, ICurrentUser u,
            IOAuthStateService states, IOptionsMonitor<AppOptions> options) =>
        {
            var uid = u.UserId!.Value;
            var aid = OpenApiApplicationId.From(id);
            var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == aid && a.UserId == uid);
            if (application is null) return Results.NotFound();
            var state = states.CreateState(uid, aid, AuthorizeStateTtl, isInvite: false);
            return Results.Ok(new { url = BuildAuthorizeUrl(options.CurrentValue.OpenApi, application, state) });
        });

        g.MapPost("/applications/{id:guid}/invite", async (Guid id, DataContext db, ICurrentUser u,
            IOAuthStateService states, HttpContext ctx) =>
        {
            var uid = u.UserId!.Value;
            var aid = OpenApiApplicationId.From(id);
            var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == aid && a.UserId == uid);
            if (application is null) return Results.NotFound();
            var state = states.CreateState(uid, aid, InviteTtl, isInvite: true);
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            return Results.Ok(new { url = $"{baseUrl}/openapi/invite/{Uri.EscapeDataString(state)}" });
        });

        app.MapGet("/openapi/invite/{state}", async (string state, DataContext db,
            IOAuthStateService states, IOptionsMonitor<AppOptions> options) =>
        {
            var result = states.Validate(state);
            if (result is null || !result.IsInvite) return Html(ErrorPage("This invite link is invalid or expired."));
            var application = await db.OpenApiApplications
                .FirstOrDefaultAsync(a => a.Id == result.ApplicationId && a.UserId == result.UserId);
            if (application is null) return Html(ErrorPage("The linked application no longer exists."));
            var authState = states.CreateState(result.UserId, result.ApplicationId, AuthorizeStateTtl, isInvite: false);
            return Results.Redirect(BuildAuthorizeUrl(options.CurrentValue.OpenApi, application, authState));
        }).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        app.MapGet("/openapi/callback", async (string? code, string? state, DataContext db,
            IOAuthStateService states, IOpenApiTokenClient tokenClient, IOpenApiClient client,
            OpenApiAccountLinker linker, ISecretProtector p, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Html(ErrorPage("Missing authorization code or state."));

            var result = states.Validate(state);
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
