using Core;
using Core.Agent;
using Core.Constants;
using Core.Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/agent").RequireAuthorization(AuthPolicies.UserOrAbove);

        g.MapGet("/mandates", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var mandates = await db.AgentMandates.Where(m => m.UserId == uid)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id.Value,
                    name = m.Name,
                    cbotId = m.CBotId.Value,
                    cbotName = m.CBot.Name,
                    tradingAccountId = m.TradingAccountId == null ? (Guid?)null : m.TradingAccountId.Value.Value,
                    objective = m.Objective,
                    riskPercentPerTrade = m.RiskPercentPerTrade,
                    maxDrawdownPercent = m.MaxDrawdownPercent,
                    symbol = m.Symbol,
                    timeframe = m.Timeframe,
                    autonomy = m.Autonomy.ToString(),
                    enabled = m.Enabled,
                    lastRunAt = m.LastRunAt
                })
                .ToListAsync(ct);
            return Results.Ok(mandates);
        });

        g.MapPost("/mandates", async (CreateMandateRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("name required");
            if (!Enum.TryParse<AgentAutonomy>(req.Autonomy, ignoreCase: true, out var autonomy))
                autonomy = AgentAutonomy.Suggest;

            var cbotId = CBotId.From(req.CBotId);
            var ownsCbot = await db.CBots.AnyAsync(c => c.Id == cbotId && c.UserId == uid, ct);
            if (!ownsCbot) return Results.BadRequest("cBot not found");

            TradingAccountId? accountId = null;
            if (req.TradingAccountId is { } acct)
            {
                var aid = TradingAccountId.From(acct);
                var ownsAccount = await db.TradingAccounts.Include(t => t.CTid)
                    .AnyAsync(t => t.Id == aid && t.CTid.UserId == uid, ct);
                if (!ownsAccount) return Results.BadRequest("trading account not found");
                accountId = aid;
            }

            var mandate = AgentMandate.Create(uid, cbotId, req.Name!.Trim(), req.Objective ?? string.Empty,
                new RiskPercent(req.RiskPercentPerTrade ?? 1.0),
                new DrawdownPercent(req.MaxDrawdownPercent ?? 20.0),
                new Symbol(string.IsNullOrWhiteSpace(req.Symbol) ? AgentConstants.DefaultSymbol : req.Symbol!),
                new Timeframe(string.IsNullOrWhiteSpace(req.Timeframe) ? AgentConstants.DefaultTimeframe : req.Timeframe!),
                new DockerImageTag(string.IsNullOrWhiteSpace(req.DockerImageTag) ? DockerImages.DefaultTag : req.DockerImageTag!),
                autonomy, req.BacktestSettingsJson, accountId);
            if (req.Enabled) mandate.Enable();
            db.AgentMandates.Add(mandate);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { id = mandate.Id.Value });
        });

        g.MapPut("/mandates/{id:guid}", async (Guid id, UpdateMandateRequest req, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var mid = AgentMandateId.From(id);
            var mandate = await db.AgentMandates.FirstOrDefaultAsync(m => m.Id == mid && m.UserId == uid, ct);
            if (mandate is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(req.Name)) mandate.Rename(req.Name!.Trim());
            if (req.Objective is not null) mandate.SetObjective(req.Objective);
            if (req.RiskPercentPerTrade is { } risk) mandate.SetRiskPerTrade(new RiskPercent(risk));
            if (req.MaxDrawdownPercent is { } dd) mandate.SetMaxDrawdown(new DrawdownPercent(dd));
            if (!string.IsNullOrWhiteSpace(req.Symbol)) mandate.SetSymbol(new Symbol(req.Symbol!));
            if (!string.IsNullOrWhiteSpace(req.Timeframe)) mandate.SetTimeframe(new Timeframe(req.Timeframe!));
            if (req.Autonomy is not null && Enum.TryParse<AgentAutonomy>(req.Autonomy, ignoreCase: true, out var autonomy))
                mandate.SetAutonomy(autonomy);
            if (req.Enabled is { } enabled) { if (enabled) mandate.Enable(); else mandate.Disable(); }
            if (req.TradingAccountId is { } acct)
            {
                var aid = TradingAccountId.From(acct);
                var ownsAccount = await db.TradingAccounts.Include(t => t.CTid)
                    .AnyAsync(t => t.Id == aid && t.CTid.UserId == uid, ct);
                if (!ownsAccount) return Results.BadRequest("trading account not found");
                mandate.SetTradingAccount(aid);
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        g.MapDelete("/mandates/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var mid = AgentMandateId.From(id);
            var mandate = await db.AgentMandates.FirstOrDefaultAsync(m => m.Id == mid && m.UserId == uid, ct);
            if (mandate is null) return Results.NotFound();
            db.AgentMandates.Remove(mandate);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/proposals", async (Guid? mandateId, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var query = db.AgentProposals.Where(p => p.UserId == uid);
            if (mandateId is { } mid) query = query.Where(p => p.MandateId == AgentMandateId.From(mid));
            var proposals = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new
                {
                    id = p.Id.Value,
                    mandateId = p.MandateId.Value,
                    mandateName = p.Mandate.Name,
                    kind = p.Kind,
                    reasoning = p.Reasoning,
                    payloadJson = p.PayloadJson,
                    proposedName = p.ProposedName,
                    status = p.Status.ToString(),
                    createdInstanceId = p.CreatedInstanceId == null ? (Guid?)null : p.CreatedInstanceId.Value.Value,
                    failureReason = p.FailureReason,
                    createdAt = p.CreatedAt,
                    decidedAt = p.DecidedAt
                })
                .ToListAsync(ct);
            return Results.Ok(proposals);
        });

        g.MapPost("/proposals/{id:guid}/approve", async (Guid id, DataContext db, ICurrentUser u, IAgentExecutor executor, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var pid = AgentProposalId.From(id);
            var proposal = await db.AgentProposals.FirstOrDefaultAsync(p => p.Id == pid && p.UserId == uid, ct);
            if (proposal is null) return Results.NotFound();
            if (proposal.Status is AgentProposalStatus.Executed) return Results.Ok(new { success = true });
            if (proposal.Status is AgentProposalStatus.Rejected) return Results.BadRequest("proposal already rejected");

            var ok = await executor.ExecuteAsync(pid, uid, ct);
            if (!ok)
            {
                var reason = await db.AgentProposals.Where(p => p.Id == pid && p.UserId == uid).Select(p => p.FailureReason).FirstOrDefaultAsync(ct);
                return Results.Ok(new { success = false, error = reason ?? "execution failed" });
            }
            return Results.Ok(new { success = true });
        });

        g.MapPost("/proposals/{id:guid}/reject", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var pid = AgentProposalId.From(id);
            var proposal = await db.AgentProposals.FirstOrDefaultAsync(p => p.Id == pid && p.UserId == uid, ct);
            if (proposal is null) return Results.NotFound();
            if (proposal.Status is AgentProposalStatus.Executed) return Results.BadRequest("proposal already executed");
            if (proposal.Status is AgentProposalStatus.Pending) proposal.Reject(uid);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { success = true });
        });

        return app;
    }
}

public sealed record CreateMandateRequest(
    Guid CBotId, Guid? TradingAccountId, string? Name, string? Objective,
    double? RiskPercentPerTrade, double? MaxDrawdownPercent, string? Symbol, string? Timeframe,
    string? DockerImageTag, string? BacktestSettingsJson, string? Autonomy, bool Enabled);

public sealed record UpdateMandateRequest(
    string? Name, string? Objective, double? RiskPercentPerTrade, double? MaxDrawdownPercent,
    string? Symbol, string? Timeframe, Guid? TradingAccountId, string? Autonomy, bool? Enabled);
