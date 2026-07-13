using System.Text.Json;
using Core;
using Core.Domain;
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

        g.MapGet("/notes", async (Core.Domain.IJournalNoteRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var notes = await repo.ListByUserAsync(uid, ct);
            return Results.Ok(notes.Select(ToNoteResponse));
        });

        g.MapPost("/notes", async (
            JournalNoteRequest req, Core.Domain.IJournalNoteRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            try
            {
                var note = Core.Journal.JournalNote.Create(uid, req.Title ?? string.Empty, req.Body, req.Symbol);
                await repo.AddAsync(note, ct);
                await repo.SaveChangesAsync(ct);
                return Results.Ok(ToNoteResponse(note));
            }
            catch (DomainException ex) { return Results.BadRequest(new { error = ex.Code }); }
        });

        g.MapPut("/notes/{id:guid}", async (
            Guid id, JournalNoteRequest req, Core.Domain.IJournalNoteRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var note = await repo.GetByIdAsync(JournalNoteId.From(id), uid, ct);
            if (note is null) return Results.NotFound();
            try
            {
                note.Edit(req.Title ?? string.Empty, req.Body, req.Symbol);
                await repo.SaveChangesAsync(ct);
                return Results.Ok(ToNoteResponse(note));
            }
            catch (DomainException ex) { return Results.BadRequest(new { error = ex.Code }); }
        });

        g.MapDelete("/notes/{id:guid}", async (
            Guid id, Core.Domain.IJournalNoteRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var note = await repo.GetByIdAsync(JournalNoteId.From(id), uid, ct);
            if (note is null) return Results.NotFound();
            repo.Remove(note);
            await repo.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }

    private static object ToNoteResponse(Core.Journal.JournalNote n) => new
    {
        id = n.Id.Value,
        title = n.Title,
        body = n.Body,
        symbol = n.Symbol,
        createdAt = n.CreatedAt,
        updatedAt = n.UpdatedAt
    };

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

public sealed record JournalNoteRequest(string? Title, string? Body, string? Symbol);
