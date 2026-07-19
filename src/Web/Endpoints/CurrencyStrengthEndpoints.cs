using Core.Ai.CurrencyStrength;
using Core.Calendar;
using Core.Domain;
using Core.Features;
using Infrastructure.Ai.CurrencyStrength;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Web.Calendar;

namespace Web.Endpoints;

/// <summary>
/// The AI macro currency-strength surface: the in-app REST API (<c>/api/ai/currency-strength</c>, cookie-auth,
/// gated on <see cref="FeatureFlag.Ai"/>) and the cBot REST API (<c>/api/market/v1/currency-strength</c>,
/// secured by the same <see cref="CalendarJwt"/> machinery with a <c>market:read</c> scope — no second JWT
/// scheme). Both serve the single shared <see cref="ICurrencyStrengthQuery"/> read model.
/// </summary>
public static class CurrencyStrengthEndpoints
{
    public static IEndpointRouteBuilder MapCurrencyStrengthEndpoints(this IEndpointRouteBuilder app)
    {
        MapInApp(app);
        MapCBot(app);
        return app;
    }

    private static void MapInApp(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ai/currency-strength")
            .RequireAuthorization("UserOrAbove")
            .RequireFeature(FeatureFlag.Ai)
            .WithAiModelOverride();

        g.MapGet("/latest", async (HttpContext http, ICurrencyStrengthQuery query, CancellationToken ct) =>
        {
            var view = await query.LatestAsync(
                ParseHorizon(http.Request.Query["horizon"]), http.Request.Query["tier"], ct);
            return view is null ? Results.NoContent() : Results.Ok(view);
        });

        g.MapGet("/history", async (HttpContext http, ICurrencyStrengthQuery query, TimeProvider time, CancellationToken ct) =>
        {
            var days = int.TryParse(http.Request.Query["days"], out var d) ? d : 30;
            return Results.Ok(await query.HistoryAsync(days, time.GetUtcNow(), ct));
        });

        g.MapPost("/refresh", async (CurrencyStrengthRefresher refresher, ICurrencyStrengthQuery query, CancellationToken ct) =>
        {
            var snapshot = await refresher.RefreshAsync(ct);
            if (snapshot is null) return Results.NoContent();
            var view = await query.LatestAsync(Horizon.ThreeMonths, null, ct);
            return view is null ? Results.NoContent() : Results.Ok(view);
        }).RequireAuthorization(Core.Constants.AuthPolicies.Owner);
    }

    private static void MapCBot(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/market/v1/currency-strength")
            .RequireFeature(FeatureFlag.Ai);

        v1.MapGet("/latest", async (HttpContext http, ICurrencyStrengthQuery query, CancellationToken ct) =>
        {
            var view = await query.LatestAsync(
                ParseHorizon(http.Request.Query["horizon"]), http.Request.Query["tier"], ct);
            return view is null ? Results.NoContent() : Results.Ok(view);
        }).RequireMarketScope();

        v1.MapGet("/history", async (HttpContext http, ICurrencyStrengthQuery query, TimeProvider time, CancellationToken ct) =>
        {
            var days = int.TryParse(http.Request.Query["days"], out var d) ? d : 30;
            return Results.Ok(await query.HistoryAsync(days, time.GetUtcNow(), ct));
        }).RequireMarketScope();

        v1.MapGet("/pair/{baseCode}/{quoteCode}", async (
            string baseCode, string quoteCode, HttpContext http, ICurrencyStrengthQuery query, CancellationToken ct) =>
        {
            var pair = await query.PairAsync(baseCode, quoteCode, ParseHorizon(http.Request.Query["horizon"]), ct);
            return pair is null ? Results.NotFound() : Results.Ok(pair);
        }).RequireMarketScope();
    }

    private static Horizon ParseHorizon(string? value)
    {
        try { return HorizonExtensions.Parse(value); }
        catch (DomainException) { return Horizon.ThreeMonths; }
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
