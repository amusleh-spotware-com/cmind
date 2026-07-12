using Core;
using Core.Agent;
using Core.Autonomy;
using Core.Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

/// <summary>
/// Agent Studio — create and run persona-driven trading agents. Lifecycle transitions and every safety
/// pre-condition (Full Auto needs a risk envelope + current disclaimer consent) are enforced by the
/// <see cref="TradingAgent"/> aggregate and the Autonomy &amp; Safety Kernel, not here.
/// </summary>
public static class AgentStudioEndpoints
{
    public static IEndpointRouteBuilder MapAgentStudioEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/agent-studio").RequireAuthorization("UserOrAbove")
            .RequireFeature(Core.Features.FeatureFlag.AgentStudio);

        g.MapGet("/", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var agents = await db.TradingAgents.Where(a => a.UserId == uid)
                .OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
            return Results.Ok(agents.Select(Roster));
        });

        g.MapGet("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var agent = await Find(db, id, uid, ct);
            return agent is null ? Results.NotFound() : Results.Ok(Detail(agent));
        });

        g.MapGet("/{id:guid}/decisions", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var aid = TradingAgentId.From(id);
            if (!await db.TradingAgents.AnyAsync(a => a.Id == aid && a.UserId == uid, ct)) return Results.NotFound();
            var records = await db.AgentDecisionRecords
                .Where(r => r.AgentId == aid && r.UserId == uid)
                .OrderByDescending(r => r.Sequence)
                .Take(200)
                .ToListAsync(ct);
            return Results.Ok(records.Select(r => new
            {
                sequence = r.Sequence,
                outcome = r.Outcome,
                reasoning = r.Reasoning,
                reason = r.Reason,
                order = r.OrderJson,
                evidence = r.EvidenceCsv,
                executed = r.Executed,
                at = r.CreatedAt
            }));
        });

        g.MapPost("/", async (CreateAgentRequest req, DataContext db, ICurrentUser u, TimeProvider time, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            try
            {
                var archetype = ParseEnum(req.Archetype, AgentArchetype.Scalper);
                var agent = TradingAgent.Create(uid, req.Name ?? "Agent", archetype,
                    new AgentTemperament(req.Aggressiveness ?? 0.5, req.Patience ?? 0.5, req.TrendBias ?? 0.5));

                if (req.AccountIds is { Length: > 0 })
                    agent.SetManagedAccounts(req.AccountIds.Select(TradingAccountId.From));
                if (req.Goals is { Length: > 0 })
                    agent.SetGoals(req.Goals.Select(ToTarget).ToList());
                if (req.Envelope is { } env)
                    agent.SetRiskEnvelope(ToEnvelope(env));
                agent.SetAutonomy(ParseEnum(req.Autonomy, AutonomyLevel.Advisory));
                if (req.AcceptDisclaimer)
                    agent.AcceptDisclaimer(time.GetUtcNow());

                db.TradingAgents.Add(agent);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { id = agent.Id.Value });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPut("/{id:guid}/goals", async (Guid id, GoalsRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
            await Mutate(db, id, u, ct, a => a.SetGoals((req.Goals ?? []).Select(ToTarget).ToList())));

        g.MapPost("/{id:guid}/start", async (Guid id, DataContext db, ICurrentUser u, TimeProvider time, CancellationToken ct) =>
            await Mutate(db, id, u, ct, a => a.Start(time.GetUtcNow())));

        g.MapPost("/{id:guid}/stop", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
            await Mutate(db, id, u, ct, a => a.Stop()));

        g.MapPost("/{id:guid}/halt", async (Guid id, DataContext db, ICurrentUser u, TimeProvider time, CancellationToken ct) =>
            await Mutate(db, id, u, ct, a => a.Halt("Kill switch", time.GetUtcNow())));

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var agent = await Find(db, id, uid, ct);
            if (agent is null) return Results.NotFound();
            db.TradingAgents.Remove(agent);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        return app;
    }

    private static async Task<IResult> Mutate(DataContext db, Guid id, ICurrentUser u, CancellationToken ct, Action<TradingAgent> action)
    {
        if (u.UserId is not { } uid) return Results.Unauthorized();
        var agent = await Find(db, id, uid, ct);
        if (agent is null) return Results.NotFound();
        try
        {
            action(agent);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Detail(agent));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Code });
        }
    }

    private static Task<TradingAgent?> Find(DataContext db, Guid id, UserId uid, CancellationToken ct)
    {
        var aid = TradingAgentId.From(id);
        return db.TradingAgents.FirstOrDefaultAsync(a => a.Id == aid && a.UserId == uid, ct);
    }

    private static object Roster(TradingAgent a) => new
    {
        id = a.Id.Value,
        name = a.Name,
        archetype = a.Archetype.ToString(),
        autonomy = a.Autonomy.ToString(),
        status = a.Status.ToString(),
        accountCount = a.ManagedAccounts.Count,
        lastAction = a.LastAction,
        lastActionAt = a.LastActionAt,
        goalCount = a.Goals.Count
    };

    private static object Detail(TradingAgent a) => new
    {
        id = a.Id.Value,
        name = a.Name,
        archetype = a.Archetype.ToString(),
        autonomy = a.Autonomy.ToString(),
        status = a.Status.ToString(),
        accountCount = a.ManagedAccounts.Count,
        goalCount = a.Goals.Count,
        systemPrompt = a.CompileSystemPrompt(),
        lastAction = a.LastAction,
        lastActionAt = a.LastActionAt,
        haltReason = a.HaltReason
    };

    private static PerformanceTarget ToTarget(AgentGoalDto g) => new(
        ParseEnum(g.Metric, TargetMetric.MaxDrawdown),
        ParseEnum(g.Comparator, TargetComparator.Below),
        g.Threshold,
        ParseEnum(g.Enforcement, TargetEnforcement.Hard));

    private static RiskEnvelope ToEnvelope(EnvelopeDto e) => new(
        e.MaxDailyLossPercent, e.MaxOpenExposureLots, e.MaxPositionSizeLots, e.MaxLeverage,
        e.MaxConsecutiveLosses, e.MaxOrdersPerHour,
        string.IsNullOrWhiteSpace(e.AllowedSymbolsCsv)
            ? null
            : e.AllowedSymbolsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet());

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}

public sealed record CreateAgentRequest(
    string? Name, string? Archetype, double? Aggressiveness, double? Patience, double? TrendBias,
    string? Autonomy, Guid[]? AccountIds, AgentGoalDto[]? Goals, EnvelopeDto? Envelope, bool AcceptDisclaimer);

public sealed record AgentGoalDto(string Metric, string Comparator, double Threshold, string Enforcement);

public sealed record EnvelopeDto(
    double MaxDailyLossPercent, double MaxOpenExposureLots, double MaxPositionSizeLots, double MaxLeverage,
    int MaxConsecutiveLosses, int MaxOrdersPerHour, string? AllowedSymbolsCsv);

public sealed record GoalsRequest(AgentGoalDto[]? Goals);
