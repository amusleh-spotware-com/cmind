using Core.Calendar;
using Core.Cot;
using Core.Features;
using Core.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Calendar;

namespace Web.Endpoints;

/// <summary>
/// The Commitment of Traders surface: the in-app REST API (<c>/api/cot</c>, cookie-auth, gated on
/// <see cref="FeatureFlag.Cot"/>) and the cBot REST API (<c>/api/market/v1/cot</c>, secured by the same
/// <see cref="CalendarJwt"/> machinery with a <c>market:read</c> scope — no second JWT scheme). Both serve the
/// single shared <see cref="ICotReports"/> read model, point-in-time correct via the optional <c>asOf</c>.
/// </summary>
public static class CotApiEndpoints
{
    public static IEndpointRouteBuilder MapCotApiEndpoints(this IEndpointRouteBuilder app)
    {
        MapInApp(app);
        MapCBot(app);
        return app;
    }

    private static void MapInApp(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cot")
            .RequireAuthorization("UserOrAbove")
            .RequireCotEnabled();

        g.MapGet("/markets", async (HttpContext http, ICotReports cot, CancellationToken ct) =>
            Results.Ok(await cot.GetMarketsAsync(ParseGroup(http.Request.Query["group"]), http.Request.Query["q"], ct)));

        g.MapGet("/latest", async (HttpContext http, ICotReports cot, CancellationToken ct) =>
            await LatestAsync(http, cot, null, ct));

        g.MapGet("/history", async (HttpContext http, ICotReports cot, TimeProvider time, CancellationToken ct) =>
            await HistoryAsync(http, cot, null, time, ct));

        g.MapGet("/health", async (ICotReports cot, CancellationToken ct) =>
            Results.Ok(await cot.GetHealthAsync(ct)));
    }

    private static void MapCBot(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/market/v1/cot")
            .RequireCotEnabled();

        v1.MapGet("/markets", async (HttpContext http, ICotReports cot, CancellationToken ct) =>
            Results.Ok(await cot.GetMarketsAsync(ParseGroup(http.Request.Query["group"]), http.Request.Query["q"], ct)))
            .RequireMarketScope();

        v1.MapGet("/latest", async (HttpContext http, ICotReports cot, CancellationToken ct) =>
            await LatestAsync(http, cot, ParseAsOf(http.Request.Query["asOf"]), ct))
            .RequireMarketScope();

        v1.MapGet("/history/{code}", async (
                string code, HttpContext http, ICotReports cot, TimeProvider time, CancellationToken ct) =>
                await HistoryForCodeAsync(code, http, cot, ParseAsOf(http.Request.Query["asOf"]), time, ct))
            .RequireMarketScope();
    }

    private static async Task<IResult> LatestAsync(
        HttpContext http, ICotReports cot, DateTimeOffset? asOf, CancellationToken ct)
    {
        var code = http.Request.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "code_required" });
        var view = await cot.GetLatestAsync(
            new ContractMarketCode(code), ParseKind(http.Request.Query["kind"]),
            ParseBool(http.Request.Query["combined"]), asOf, ct);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> HistoryAsync(
        HttpContext http, ICotReports cot, DateTimeOffset? asOf, TimeProvider time, CancellationToken ct)
    {
        var code = http.Request.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "code_required" });
        return await HistoryForCodeAsync(code, http, cot, asOf, time, ct);
    }

    private static async Task<IResult> HistoryForCodeAsync(
        string code, HttpContext http, ICotReports cot, DateTimeOffset? asOf, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var from = ParseAsOf(http.Request.Query["from"]) ?? now.AddYears(-3);
        var to = ParseAsOf(http.Request.Query["to"]) ?? now;
        var points = await cot.GetHistoryAsync(
            new ContractMarketCode(code), ParseKind(http.Request.Query["kind"]),
            ParseBool(http.Request.Query["combined"]), from, to, asOf, ct);
        return Results.Ok(points);
    }

    private static CotReportKind ParseKind(string? value)
        => Enum.TryParse<CotReportKind>(value, ignoreCase: true, out var kind) ? kind : CotReportKind.Legacy;

    private static CotContractGroup? ParseGroup(string? value)
        => Enum.TryParse<CotContractGroup>(value, ignoreCase: true, out var group) ? group : null;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var b) && b;

    private static DateTimeOffset? ParseAsOf(string? value)
        => DateTimeOffset.TryParse(value, out var instant) ? instant : null;

    /// <summary>404s the whole COT tree unless both the white-label gate and the runtime toggle allow it.</summary>
    private static TBuilder RequireCotEnabled<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppOptions>>();
            var gate = context.HttpContext.RequestServices.GetRequiredService<IFeatureGate>();
            return CotEnablement.IsEnabled(options.CurrentValue.Branding, gate)
                ? await next(context)
                : Results.NotFound();
        });
        return builder;
    }

    /// <summary>Validates the bearer JWT and enforces the <c>market:read</c> scope — reusing the calendar's
    /// <see cref="CalendarJwt"/> (one scheme, one secret). 401 on a bad token, 403 on a missing scope.</summary>
    private static RouteHandlerBuilder RequireMarketScope(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var jwt = context.HttpContext.RequestServices.GetRequiredService<CalendarJwt>();
            var header = context.HttpContext.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : null;

            var principal = await jwt.ValidateAsync(token, context.HttpContext.RequestAborted);
            if (principal is null)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
            if (!CalendarJwt.ScopesOf(principal).Contains(CalendarScopes.MarketRead))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "insufficient_scope");

            return await next(context);
        });
        return builder;
    }
}
