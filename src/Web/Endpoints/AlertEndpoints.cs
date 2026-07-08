using Core;
using Core.Constants;
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
        var g = app.MapGroup("/api/alerts").RequireAuthorization(AuthPolicies.UserOrAbove);

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

            var rule = new AlertRule
            {
                UserId = uid,
                Name = req.Name!.Trim(),
                Symbol = req.Symbol!.Trim().ToUpperInvariant(),
                IntervalMinutes = Math.Clamp(req.IntervalMinutes ?? AlertConstants.DefaultIntervalMinutes, AlertConstants.MinIntervalMinutes, AlertConstants.MaxIntervalMinutes),
                Enabled = req.Enabled ?? true
            };
            db.AlertRules.Add(rule);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Conflict("a rule with that name already exists"); }
            return Results.Ok(new { id = rule.Id.Value });
        });

        g.MapPut("/rules/{id:guid}", async (Guid id, UpdateAlertRuleRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var rid = AlertRuleId.From(id);
            var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == rid && r.UserId == uid, ct);
            if (rule is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) rule.Name = req.Name!.Trim();
            if (!string.IsNullOrWhiteSpace(req.Symbol)) rule.Symbol = req.Symbol!.Trim().ToUpperInvariant();
            if (req.IntervalMinutes is { } minutes) rule.IntervalMinutes = Math.Clamp(minutes, AlertConstants.MinIntervalMinutes, AlertConstants.MaxIntervalMinutes);
            if (req.Enabled is { } enabled) rule.Enabled = enabled;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
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
            evt.Acknowledged = true;
            evt.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        return app;
    }
}

public sealed record CreateAlertRuleRequest(string? Name, string? Symbol, int? IntervalMinutes, bool? Enabled);
public sealed record UpdateAlertRuleRequest(string? Name, string? Symbol, int? IntervalMinutes, bool? Enabled);
