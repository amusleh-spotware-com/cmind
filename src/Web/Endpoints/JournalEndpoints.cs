using System.Text.Json;
using Core;
using Core.Journal;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

/// <summary>
/// Trading Journal — analyzes the user's own instances (runs and backtests) for behavioural leaks
/// (over-concentration, repeated failures, a losing bias). Deterministic; the AI coach (Portfolio Digest)
/// layers narrative on top.
/// </summary>
public static class JournalEndpoints
{
    private const int MaxEntries = 100;
    private static readonly string[] ProfitKeys = ["netProfit", "totalNetProfit", "netProfitAmount", "profit"];

    public static IEndpointRouteBuilder MapJournalEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/journal").RequireAuthorization("UserOrAbove");

        g.MapGet("/", async (DataContext db, ICurrentUser u, IJournalAnalyzer analyzer, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();

            var instances = await db.Instances.Include(i => i.CBot)
                .Where(i => i.UserId == uid)
                .OrderByDescending(i => i.CreatedAt)
                .Take(MaxEntries)
                .ToListAsync(ct);

            var entries = instances.Select(ToEntry).ToList();
            var summary = analyzer.Analyze(entries);

            return Results.Ok(new
            {
                summary = new
                {
                    total = summary.Total,
                    wins = summary.Wins,
                    losses = summary.Losses,
                    failures = summary.Failures,
                    winRate = summary.WinRate,
                    insights = summary.Insights
                },
                entries = instances.Select(i => new
                {
                    cbot = i.CBot.Name,
                    symbol = i.Symbol,
                    kind = i.KindName,
                    status = i.StatusName
                })
            });
        });

        return app;
    }

    private static JournalEntry ToEntry(Instance i)
    {
        var outcome = i switch
        {
            FailedRunInstance or FailedBacktestInstance => TradeOutcome.Failed,
            CompletedBacktestInstance => TradeOutcome.Completed,
            _ => TradeOutcome.Running
        };
        var netProfit = i is CompletedBacktestInstance c ? ParseNetProfit(c.ReportJson) : null;
        return new JournalEntry(i.Symbol, i.KindName, outcome, netProfit);
    }

    private static double? ParseNetProfit(string? reportJson)
    {
        if (string.IsNullOrWhiteSpace(reportJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(reportJson);
            return FindNumber(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double? FindNumber(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in ProfitKeys)
                if (element.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number)
                    return v.GetDouble();
            foreach (var prop in element.EnumerateObject())
            {
                var nested = FindNumber(prop.Value);
                if (nested is not null) return nested;
            }
        }
        return null;
    }
}
