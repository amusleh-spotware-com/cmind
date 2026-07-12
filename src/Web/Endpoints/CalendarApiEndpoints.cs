using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core;
using Core.Calendar;
using Core.Features;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Calendar;

namespace Web.Endpoints;

public sealed record CalendarTokenRequest(string ClientId, string ClientSecret);

public sealed record CreateCalendarClientRequest(string Name, string[] Scopes, int? ExpiresInDays);

public sealed record CreateCalendarWebhookRequest(string? Url, string? Secret, string? MinImpact, string? Currencies);

public sealed record CalendarBatchItem(
    DateTimeOffset? From, DateTimeOffset? To, string[]? Countries, string[]? Currencies,
    string[]? Series, string? MinImpact, string? Q, DateTimeOffset? AsOf, int? Limit)
{
    public CalendarQuery ToQuery() => new()
    {
        From = From?.ToUniversalTime(),
        To = To?.ToUniversalTime(),
        Countries = Countries,
        Currencies = Currencies,
        Series = Series,
        MinImpact = Enum.TryParse<ImpactLevel>(MinImpact, ignoreCase: true, out var impact) ? impact : null,
        Keyword = string.IsNullOrWhiteSpace(Q) ? null : Q,
        AsOf = AsOf?.ToUniversalTime(),
        Limit = Limit is { } limit ? Math.Clamp(limit, 1, 1000) : 200
    };
}

/// <summary>
/// The public, versioned, JWT-secured Calendar REST API (<c>/api/calendar/v1</c>) plus the owner-only admin
/// surface to issue and revoke API clients. The whole tree 404s when the calendar is disabled (either the
/// white-label hard gate or the runtime toggle). Reads never touch external HTTP.
/// </summary>
public static class CalendarApiEndpoints
{
    public static IEndpointRouteBuilder MapCalendarApiEndpoints(this IEndpointRouteBuilder app)
    {
        MapAdmin(app);
        MapV1(app);
        return app;
    }

    private static void MapAdmin(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/calendar/clients")
            .RequireAuthorization()
            .RequireCalendarEnabled();

        admin.MapGet("/", async (DataContext db, ICurrentUser user) =>
        {
            var ownerId = user.UserId!.Value;
            var clients = await db.CalendarApiClients
                .Where(c => c.OwnerId == ownerId)
                .Select(c => new { c.Id, c.Name, c.KeyPrefix, c.ScopesCsv, c.ExpiresAt, c.DisabledAt, c.CreatedAt })
                .ToListAsync();
            return Results.Ok(clients);
        });

        admin.MapPost("/", async (
            CreateCalendarClientRequest request, DataContext db, ICurrentUser user, TimeProvider timeProvider) =>
        {
            if (user.UserId is not { } ownerId) return Results.Unauthorized();

            var raw = "calk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var prefix = raw[..16];
            var hash = Hash(raw);
            var expiresAt = request.ExpiresInDays is { } days and > 0
                ? timeProvider.GetUtcNow().AddDays(days)
                : (DateTimeOffset?)null;

            var client = CalendarApiClient.Create(ownerId, request.Name, request.Scopes, prefix, hash, expiresAt);
            db.CalendarApiClients.Add(client);
            await db.SaveChangesAsync();

            return Results.Ok(new { clientId = prefix, clientSecret = raw, id = client.Id, scopes = client.Scopes });
        });

        admin.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser user, TimeProvider timeProvider) =>
        {
            var ownerId = user.UserId!.Value;
            var clientId = CalendarApiClientId.From(id);
            var client = await db.CalendarApiClients.FirstOrDefaultAsync(c => c.Id == clientId && c.OwnerId == ownerId);
            if (client is null) return Results.NotFound();
            client.Disable(timeProvider.GetUtcNow());
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var webhooks = app.MapGroup("/api/calendar/webhooks")
            .RequireAuthorization()
            .RequireCalendarEnabled();

        webhooks.MapGet("/", async (DataContext db, ICurrentUser user) =>
        {
            var ownerId = user.UserId!.Value;
            var list = await db.CalendarWebhooks
                .Where(w => w.OwnerId == ownerId)
                .Select(w => new { w.Id, w.Url, w.MinImpactLevel, w.Currencies, w.DisabledAt, w.CreatedAt })
                .ToListAsync();
            return Results.Ok(list);
        });

        webhooks.MapPost("/", async (
            CreateCalendarWebhookRequest request, DataContext db, ICurrentUser user, ISecretProtector protector) =>
        {
            if (user.UserId is not { } ownerId) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Secret)) return Results.BadRequest(new { error = "secret_required" });

            var impact = ParseImpact(request.MinImpact) ?? ImpactLevel.Low;
            try
            {
                var encrypted = protector.Protect(
                    Encoding.UTF8.GetBytes(request.Secret), Core.Constants.EncryptionPurposes.CalendarWebhookSecret);
                var webhook = CalendarWebhook.Create(ownerId, request.Url ?? "", encrypted, impact, request.Currencies);
                db.CalendarWebhooks.Add(webhook);
                await db.SaveChangesAsync();
                return Results.Ok(new { id = webhook.Id });
            }
            catch (Core.Domain.DomainException ex) { return Results.BadRequest(new { error = ex.Code }); }
        });

        webhooks.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser user, TimeProvider timeProvider) =>
        {
            var ownerId = user.UserId!.Value;
            var webhookId = CalendarWebhookId.From(id);
            var webhook = await db.CalendarWebhooks.FirstOrDefaultAsync(w => w.Id == webhookId && w.OwnerId == ownerId);
            if (webhook is null) return Results.NotFound();
            webhook.Disable(timeProvider.GetUtcNow());
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void MapV1(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/calendar/v1").RequireCalendarEnabled();

        v1.MapPost("/token", async (
            CalendarTokenRequest request, DataContext db, CalendarJwt jwt, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var client = await db.CalendarApiClients.FirstOrDefaultAsync(c => c.KeyPrefix == request.ClientId, ct);
            if (client is null || !client.IsActive(timeProvider.GetUtcNow())
                || !FixedTimeEquals(client.KeyHash, Hash(request.ClientSecret)))
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_client");

            var (token, expiresAt) = await jwt.IssueAsync(client, ct);
            return Results.Ok(new { token, expiresAt, scopes = client.Scopes });
        });

        v1.MapGet("/events", async (HttpContext http, IEconomicCalendar calendar, CancellationToken ct) =>
        {
            var query = ParseQuery(http.Request);
            var events = await calendar.GetEventsAsync(query, ct);
            SetNextLink(http, events, query.Limit);
            return Cacheable(http, events);
        }).RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/events/{id:guid}", async (
            Guid id, HttpContext http, IEconomicCalendar calendar, CancellationToken ct) =>
        {
            var watchlist = SplitCsv(http.Request.Query["watchlist"]);
            var asOf = ParseInstant(http.Request.Query["asOf"]);
            var view = await calendar.GetEventAsync(CalendarEventId.From(id), watchlist, asOf, ct);
            return view is null ? Results.NotFound() : Results.Ok(view);
        }).RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/history", async (HttpContext http, IEconomicCalendar calendar, CancellationToken ct) =>
        {
            var query = ParseQuery(http.Request);
            var events = await calendar.GetEventsAsync(query, ct);
            SetNextLink(http, events, query.Limit);
            return Cacheable(http, events);
        }).RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/series", async (HttpContext http, IEconomicCalendar calendar, CancellationToken ct) =>
            Results.Ok(await calendar.GetSeriesAsync(ParseQuery(http.Request), ct)))
            .RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/surprises", async (HttpContext http, IEconomicCalendar calendar, CancellationToken ct) =>
        {
            var series = http.Request.Query["series"].ToString();
            if (string.IsNullOrWhiteSpace(series)) return Results.BadRequest();
            var count = int.TryParse(http.Request.Query["count"], out var c) ? c : 24;
            var asOf = ParseInstant(http.Request.Query["asOf"]);
            return Results.Ok(await calendar.GetSurprisesAsync(new SeriesCode(series), count, asOf, ct));
        }).RequireCalendarScope(CalendarScopes.Surprises);

        v1.MapGet("/next", async (
            HttpContext http, IEconomicCalendar calendar, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var symbol = http.Request.Query["symbol"].ToString();
            if (string.IsNullOrWhiteSpace(symbol)) return Results.BadRequest();
            var minImpact = ParseImpact(http.Request.Query["minImpact"]) ?? ImpactLevel.Low;
            var view = await calendar.GetNextForSymbolAsync(
                new Symbol(symbol), minImpact, timeProvider.GetUtcNow(), ct);
            return view is null ? Results.NoContent() : Results.Ok(view);
        }).RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/for-symbol", async (
            HttpContext http, IEconomicCalendar calendar, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var symbol = http.Request.Query["symbol"].ToString();
            if (string.IsNullOrWhiteSpace(symbol)) return Results.BadRequest();
            var now = timeProvider.GetUtcNow();
            var from = ParseInstant(http.Request.Query["from"]) ?? now.AddDays(-30);
            var to = ParseInstant(http.Request.Query["to"]) ?? now.AddDays(30);
            var asOf = ParseInstant(http.Request.Query["asOf"]);
            return Results.Ok(await calendar.GetEventsForSymbolAsync(new Symbol(symbol), from, to, asOf, ct));
        }).RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/blackout", async (
            HttpContext http, IEconomicCalendar calendar, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var symbol = http.Request.Query["symbol"].ToString();
            if (string.IsNullOrWhiteSpace(symbol)) return Results.BadRequest();
            var at = ParseInstant(http.Request.Query["at"]) ?? timeProvider.GetUtcNow();
            var minImpact = ParseImpact(http.Request.Query["minImpact"]) ?? ImpactLevel.High;
            var before = int.TryParse(http.Request.Query["before"], out var b) ? b : 15;
            var after = int.TryParse(http.Request.Query["after"], out var a) ? a : 15;
            var rule = new NewsWindowRule(minImpact, before, after);
            var result = await calendar.GetBlackoutAsync(new Symbol(symbol), at, rule, ct);
            return Results.Ok(result);
        }).RequireCalendarScope(CalendarScopes.Blackout);

        v1.MapGet("/affected-symbols", async (
            HttpContext http, IEconomicCalendar calendar, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.Request.Query["eventId"], out var eventId)) return Results.BadRequest();
            var watchlist = SplitCsv(http.Request.Query["watchlist"]) ?? [];
            var symbols = await calendar.GetAffectedSymbolsAsync(CalendarEventId.From(eventId), watchlist, ct);
            return Results.Ok(symbols.Select(s => s.Value));
        }).RequireCalendarScope(CalendarScopes.Read);

        v1.MapGet("/health", async (IEconomicCalendar calendar, CancellationToken ct) =>
            Results.Ok(await calendar.GetHealthAsync(ct)))
            .RequireCalendarScope(CalendarScopes.Read);

        // Live push (Server-Sent Events): emits released events matching the filter as they appear, plus a
        // heartbeat. Poll-backed (no cross-process bus); bounded lifetime; ends on client disconnect.
        v1.MapGet("/stream", async (HttpContext http, IEconomicCalendar calendar, TimeProvider time, CancellationToken ct) =>
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            var currencies = SplitCsv(http.Request.Query["currencies"]);
            var minImpact = ParseImpact(http.Request.Query["minImpact"]) ?? ImpactLevel.Low;
            var seen = new HashSet<Guid>();
            var deadline = time.GetUtcNow().AddMinutes(30);

            while (!ct.IsCancellationRequested && time.GetUtcNow() < deadline)
            {
                var now = time.GetUtcNow();
                var recent = await calendar.GetEventsAsync(new CalendarQuery
                {
                    From = now.AddDays(-1), To = now, Currencies = currencies, MinImpact = minImpact, Limit = 50
                }, ct);

                foreach (var e in recent.Where(e => e.Released && seen.Add(e.Id.Value)))
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        id = e.Id.Value, seriesCode = e.SeriesCode, country = e.Country,
                        effectiveAt = e.EffectiveAt, actual = e.Actual, impact = e.Impact.ToString()
                    });
                    await http.Response.WriteAsync($"event: release\ndata: {payload}\n\n", ct);
                }

                await http.Response.WriteAsync(": heartbeat\n\n", ct);
                await http.Response.Body.FlushAsync(ct);

                try { await Task.Delay(TimeSpan.FromSeconds(2), time, ct); }
                catch (OperationCanceledException) { break; }
            }
        }).RequireCalendarScope(CalendarScopes.Stream);

        // Discoverable, versioned contract — no scope required (it is just the schema).
        v1.MapGet("/openapi.json", () => Results.Json(CalendarOpenApi.Document));

        // Multiplex several event queries in one round-trip (cBot efficiency); bounded fan-out.
        v1.MapPost("/events/batch", async (
            CalendarBatchItem[] queries, IEconomicCalendar calendar, CancellationToken ct) =>
        {
            if (queries.Length is 0 or > 20) return Results.BadRequest(new { error = "batch_size" });
            var results = new List<object>(queries.Length);
            foreach (var query in queries)
                results.Add(new { events = await calendar.GetEventsAsync(query.ToQuery(), ct) });
            return Results.Ok(results);
        }).RequireCalendarScope(CalendarScopes.Read);
    }

    private static CalendarQuery ParseQuery(HttpRequest request)
    {
        var q = request.Query;
        return new CalendarQuery
        {
            From = ParseInstant(q["from"]),
            To = ParseInstant(q["to"]),
            Countries = SplitCsv(q["countries"]),
            Currencies = SplitCsv(q["currencies"]),
            Series = SplitCsv(q["series"]),
            MinImpact = ParseImpact(q["minImpact"]),
            Keyword = string.IsNullOrWhiteSpace(q["q"]) ? null : q["q"].ToString(),
            AsOf = ParseInstant(q["asOf"]),
            Limit = int.TryParse(q["limit"], out var limit) ? Math.Clamp(limit, 1, 1000) : 200
        };
    }

    private static string[]? SplitCsv(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static DateTimeOffset? ParseInstant(string? value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var instant)
            ? instant
            : null;

    private static ImpactLevel? ParseImpact(string? value) =>
        Enum.TryParse<ImpactLevel>(value, ignoreCase: true, out var level) ? level : null;

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>Emits a keyset <c>Link: rel="next"</c> when the page is full, so a client can walk deep history.</summary>
    private static void SetNextLink(HttpContext http, IReadOnlyList<CalendarEventView> events, int limit)
    {
        if (events.Count == 0 || events.Count < limit) return;
        var last = events[^1];
        var cursor = CalendarCursor.Encode(last.EffectiveAt, last.Id.Value);
        http.Response.Headers.Link = $"<{http.Request.Path}?cursor={Uri.EscapeDataString(cursor)}>; rel=\"next\"";
    }

    /// <summary>
    /// Serves an event list with a weak <c>ETag</c> derived from the result (ids + impact + actual + instant),
    /// honouring <c>If-None-Match</c> with a <c>304</c> so a backtest hammering history transfers bytes once.
    /// </summary>
    private static IResult Cacheable(HttpContext http, IReadOnlyList<CalendarEventView> events)
    {
        var basis = string.Join('|', events.Select(e => $"{e.Id.Value}:{e.ImpactScore}:{e.Actual}:{e.EffectiveAt.Ticks}"));
        var etag = "\"cal-" + Hash(basis)[..16] + "\"";
        if (string.Equals(http.Request.Headers.IfNoneMatch.ToString(), etag, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status304NotModified);
        http.Response.Headers.ETag = etag;
        return Results.Ok(events);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    /// <summary>404s the whole calendar tree unless both the white-label gate and the runtime toggle allow it.</summary>
    private static TBuilder RequireCalendarEnabled<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppOptions>>();
            var gate = context.HttpContext.RequestServices.GetRequiredService<IFeatureGate>();
            return CalendarEnablement.IsEnabled(options.CurrentValue.Branding, gate)
                ? await next(context)
                : Results.NotFound();
        });
        return builder;
    }

    /// <summary>Validates the bearer JWT and enforces the required scope; 401 otherwise.</summary>
    private static RouteHandlerBuilder RequireCalendarScope(this RouteHandlerBuilder builder, string scope)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var jwt = context.HttpContext.RequestServices.GetRequiredService<CalendarJwt>();
            var header = context.HttpContext.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : null;

            var principal = await jwt.ValidateAsync(token, context.HttpContext.RequestAborted);
            if (principal is null)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
            if (!CalendarJwt.ScopesOf(principal).Contains(scope))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "insufficient_scope");

            return await next(context);
        });
        return builder;
    }
}
