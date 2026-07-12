using Core;
using Core.Constants;
using Core.Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/alerts").RequireAuthorization(AuthPolicies.UserOrAbove)
            .RequireFeature(Core.Features.FeatureFlag.Alerts);

        g.MapGet("/rules", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rules = await db.AlertRules.Where(r => r.UserId == uid)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id.Value,
                    name = r.Name,
                    symbol = r.Symbol,
                    intervalMinutes = r.IntervalMinutes,
                    enabled = r.Enabled,
                    lastEvaluatedAt = r.LastEvaluatedAt
                })
                .ToListAsync(ct);
            return Results.Ok(rules);
        });

        g.MapPost("/rules", async (CreateAlertRuleRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("name required");
            if (string.IsNullOrWhiteSpace(req.Symbol)) return Results.BadRequest("symbol required");

            var minutes = Math.Clamp(req.IntervalMinutes ?? AlertConstants.DefaultIntervalMinutes,
                AlertConstants.MinIntervalMinutes, AlertConstants.MaxIntervalMinutes);
            var rule = AlertRule.Create(uid, req.Name!.Trim(), new Symbol(req.Symbol!),
                new EvaluationInterval(minutes));
            if (req.Enabled == false) rule.Disable();
            db.AlertRules.Add(rule);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Conflict("a rule with that name already exists"); }
            return Results.Ok(new { id = rule.Id.Value });
        });

        g.MapPost("/rules/economic-event", async (
            CreateEconomicAlertRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("name required");

            var impact = Enum.TryParse<Core.Calendar.ImpactLevel>(req.MinImpact, ignoreCase: true, out var parsed)
                ? parsed
                : Core.Calendar.ImpactLevel.High;
            var minutes = Math.Clamp(req.IntervalMinutes ?? AlertConstants.DefaultIntervalMinutes,
                AlertConstants.MinIntervalMinutes, AlertConstants.MaxIntervalMinutes);
            var before = Math.Clamp(req.MinutesBefore ?? 60, 1, 10080);

            try
            {
                var rule = AlertRule.CreateEconomicEvent(
                    uid, req.Name!.Trim(), impact, before, req.Currencies, new EvaluationInterval(minutes));
                if (req.Enabled == false) rule.Disable();
                db.AlertRules.Add(rule);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { id = rule.Id.Value });
            }
            catch (Core.Domain.DomainException ex) { return Results.BadRequest(new { error = ex.Code }); }
            catch (DbUpdateException) { return Results.Conflict("a rule with that name already exists"); }
        });

        g.MapPut("/rules/{id:guid}", async (Guid id, UpdateAlertRuleRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rid = AlertRuleId.From(id);
            var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == rid && r.UserId == uid, ct);
            if (rule is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) rule.Rename(req.Name!.Trim());
            if (!string.IsNullOrWhiteSpace(req.Symbol)) rule.SetSymbol(new Symbol(req.Symbol!));
            if (req.IntervalMinutes is { } minutes)
                rule.SetInterval(new EvaluationInterval(Math.Clamp(minutes,
                    AlertConstants.MinIntervalMinutes, AlertConstants.MaxIntervalMinutes)));
            if (req.Enabled is { } enabled) { if (enabled) rule.Enable(); else rule.Disable(); }
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        g.MapDelete("/rules/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rid = AlertRuleId.From(id);
            var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == rid && r.UserId == uid, ct);
            if (rule is null) return Results.NotFound();
            db.AlertRules.Remove(rule);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/events", async (bool? unacknowledged, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var query = db.AlertEvents.Where(e => e.UserId == uid);
            if (unacknowledged == true) query = query.Where(e => !e.Acknowledged);
            var events = await query
                .OrderByDescending(e => e.CreatedAt)
                .Take(200)
                .Select(e => new
                {
                    id = e.Id.Value,
                    ruleId = e.RuleId.Value,
                    ruleName = e.Rule.Name,
                    symbol = e.Rule.Symbol,
                    severity = e.Severity,
                    message = e.Message,
                    acknowledged = e.Acknowledged,
                    createdAt = e.CreatedAt
                })
                .ToListAsync(ct);
            return Results.Ok(events);
        });

        g.MapPost("/events/{id:guid}/ack", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var eid = AlertEventId.From(id);
            var evt = await db.AlertEvents.FirstOrDefaultAsync(e => e.Id == eid && e.UserId == uid, ct);
            if (evt is null) return Results.NotFound();
            evt.Acknowledge();
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        return app;
    }
}

public sealed record CreateAlertRuleRequest(string? Name, string? Symbol, int? IntervalMinutes, bool? Enabled);
public sealed record CreateEconomicAlertRequest(
    string? Name, string? MinImpact, int? MinutesBefore, string? Currencies, int? IntervalMinutes, bool? Enabled);
public sealed record UpdateAlertRuleRequest(string? Name, string? Symbol, int? IntervalMinutes, bool? Enabled);
