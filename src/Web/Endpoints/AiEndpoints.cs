using Core;
using Core.Ai;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ai").RequireAuthorization("UserOrAbove");

        g.MapPost("/generate", async (GenerateCBotRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.GenerateCBotAsync(req.Language ?? "CSharp", req.Description ?? "", ct)));

        g.MapPost("/review", async (ReviewCBotRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.ReviewCBotAsync(req.Language ?? "CSharp", req.Source ?? "", ct)));

        g.MapPost("/sentiment", async (SentimentRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.MarketSentimentAsync(req.Symbol ?? "", ct)));

        g.MapPost("/vision", async (VisionRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.VisionToStrategyAsync(
                new AiImage(req.MediaType ?? "image/png", req.Base64 ?? ""), req.Note, ct)));

        g.MapPost("/curate", async (CurateRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.CurateStrategyAsync(req.Name ?? "cBot", req.Language ?? "CSharp", req.Source ?? "", ct)));

        g.MapPost("/analyze-backtest/{id:guid}", async (
            Guid id, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var bt = await db.Instances.OfType<CompletedBacktestInstance>()
                .Where(i => i.Id == iid && i.UserId == uid)
                .Select(i => new { i.ReportJson, Name = i.CBot.Name })
                .FirstOrDefaultAsync(ct);
            if (bt is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(bt.ReportJson)) return Results.BadRequest("no backtest report available");
            return Results.Ok(await ai.AnalyzeBacktestAsync(bt.Name, bt.ReportJson!, ct));
        });

        g.MapPost("/optimize-params/{cbotId:guid}", async (
            Guid cbotId, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var cid = CBotId.From(cbotId);
            var name = await db.CBots.Where(c => c.Id == cid && c.UserId == uid)
                .Select(c => c.Name).FirstOrDefaultAsync(ct);
            if (name is null) return Results.NotFound();
            var current = await db.ParamSets.Where(p => p.CBotId == cid && p.UserId == uid)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.JsonContent).FirstOrDefaultAsync(ct) ?? "{}";
            return Results.Ok(await ai.ProposeParamSetsAsync(name, current, null, ct));
        });

        g.MapPost("/post-mortem/{id:guid}", async (
            Guid id, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var instance = await db.Instances.Include(i => i.CBot)
                .FirstOrDefaultAsync(i => i.Id == iid && i.UserId == uid, ct);
            if (instance is null) return Results.NotFound();
            var context = new AiInstanceContext(
                instance.CBot.Name, instance.KindName, instance.StatusName,
                instance.Symbol, instance.Timeframe, DetailOf(instance));
            return Results.Ok(await ai.PostMortemAsync(context, ct));
        });

        return app;
    }

    private static string? DetailOf(Instance instance) => instance switch
    {
        FailedRunInstance f => $"Failure: {f.FailureReason}",
        FailedBacktestInstance f => $"Failure: {f.FailureReason}",
        CompletedBacktestInstance c => c.ReportJson is null ? null : $"Report JSON:\n{c.ReportJson}",
        _ => null
    };
}

public sealed record GenerateCBotRequest(string? Language, string? Description);
public sealed record ReviewCBotRequest(string? Language, string? Source);
public sealed record SentimentRequest(string? Symbol);
public sealed record VisionRequest(string? MediaType, string? Base64, string? Note);
public sealed record CurateRequest(string? Name, string? Language, string? Source);
