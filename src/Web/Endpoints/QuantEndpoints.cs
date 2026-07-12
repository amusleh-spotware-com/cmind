using Core;
using Core.Constants;
using Core.Domain;
using Core.Portfolio;
using Core.Quant;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nodes;

namespace Web.Endpoints;

/// <summary>
/// Backtest Integrity Lab — deterministic, fund-grade overfitting statistics on a return series or a
/// completed backtest. No AI, no external calls; a raw Sharpe becomes a Robust / Fragile / Overfit verdict.
/// </summary>
public static class QuantEndpoints
{
    public static IEndpointRouteBuilder MapQuantEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/quant").RequireAuthorization("UserOrAbove");

        g.MapPost("/integrity", (IntegrityRequest req, IBacktestIntegrityAnalyzer analyzer) =>
        {
            try
            {
                var series = BuildSeries(req.Returns, req.Equity);
                var trials = new TrialCount(req.Trials is > 0 ? req.Trials.Value : 1);
                var report = analyzer.Analyze(
                    series, trials,
                    req.BenchmarkSharpe ?? 0.0,
                    req.PeriodsPerYear is > 0 ? req.PeriodsPerYear.Value : 252.0);
                return Results.Ok(IntegrityResponse.From(report));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/integrity/backtest/{id:guid}", async (
            Guid id, IntegrityBacktestRequest? req, DataContext db, ICurrentUser u,
            IBacktestIntegrityAnalyzer analyzer, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var row = await db.Instances.OfType<CompletedBacktestInstance>()
                .Where(i => i.Id == iid && i.UserId == uid)
                .Select(i => new { i.ReportJson })
                .FirstOrDefaultAsync(ct);
            if (row is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(row.ReportJson))
                return Results.BadRequest(new { error = "no backtest report available" });

            var equity = ContainerCommandHelpers.ParseEquityCurve(row.ReportJson).Select(p => p.Value).ToList();
            try
            {
                var series = ReturnSeries.FromEquityCurve(equity);
                var trials = new TrialCount(req?.Trials is > 0 ? req.Trials.Value : 1);
                return Results.Ok(IntegrityResponse.From(analyzer.Analyze(series, trials)));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/health", (IntegrityRequest req, Core.Health.IStrategyHealthMonitor monitor) =>
        {
            try
            {
                var series = BuildSeries(req.Returns, req.Equity);
                var report = monitor.Assess(series);
                return Results.Ok(new
                {
                    health = report.Health.ToString(),
                    earlierSharpe = report.EarlierSharpe,
                    recentSharpe = report.RecentSharpe,
                    changePointIndex = report.ChangePointIndex,
                    observations = report.Observations,
                    rationale = report.Rationale
                });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/pbo", (PboRequest req, IBacktestIntegrityAnalyzer analyzer) =>
        {
            if (req.Trials is not { Length: >= 2 })
                return Results.BadRequest(new { error = DomainErrors.TrialSurfaceInvalid });
            try
            {
                var surface = TrialSurface.From(req.Trials.Select(t => ReturnSeries.From(t)).ToList());
                var slices = req.Slices is > 1 ? req.Slices.Value : 8;
                return Results.Ok(IntegrityResponse.From(analyzer.AnalyzeGrid(surface, slices)));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/sizing", (SizingRequest req, IPositionSizer sizer) =>
        {
            try
            {
                var series = BuildSeries(req.Returns, req.Equity);
                var result = sizer.Size(
                    series,
                    new VolatilityTarget(req.TargetVolatility ?? 0.10),
                    new KellyFraction(req.KellyFraction ?? 0.5),
                    new LeverageCap(req.LeverageCap ?? 3.0),
                    req.PeriodsPerYear is > 0 ? req.PeriodsPerYear.Value : 252.0);
                return Results.Ok(result);
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/portfolio", (PortfolioRequest req, IPortfolioAllocator allocator) =>
        {
            if (req.Strategies is not { Length: >= 2 })
                return Results.BadRequest(new { error = DomainErrors.PortfolioInsufficientStrategies });
            try
            {
                var series = req.Strategies.Select(s => ReturnSeries.From(s)).ToList();
                var result = allocator.Allocate(
                    series,
                    new VolatilityTarget(req.TargetVolatility ?? 0.10),
                    new LeverageCap(req.LeverageCap ?? 3.0),
                    req.PeriodsPerYear is > 0 ? req.PeriodsPerYear.Value : 252.0);
                return Results.Ok(result);
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        return app;
    }

    private static ReturnSeries BuildSeries(double[]? returns, double[]? equity)
    {
        if (returns is { Length: >= 2 }) return ReturnSeries.From(returns);
        if (equity is { Length: >= 2 }) return ReturnSeries.FromEquityCurve(equity);
        throw new DomainException(DomainErrors.ReturnSeriesTooShort);
    }
}

public sealed record IntegrityRequest(
    double[]? Returns, double[]? Equity, int? Trials, double? BenchmarkSharpe, double? PeriodsPerYear);

public sealed record IntegrityBacktestRequest(int? Trials);

public sealed record PboRequest(double[][]? Trials, int? Slices);

public sealed record SizingRequest(
    double[]? Returns, double[]? Equity, double? TargetVolatility, double? KellyFraction,
    double? LeverageCap, double? PeriodsPerYear);

public sealed record PortfolioRequest(
    double[][]? Strategies, double? TargetVolatility, double? LeverageCap, double? PeriodsPerYear);

public sealed record IntegrityResponse(
    string verdict,
    double sharpe,
    double annualizedSharpe,
    double probabilisticSharpe,
    double deflatedSharpe,
    double tStatistic,
    double skewness,
    double kurtosis,
    int observations,
    int trials,
    double? probabilityOfBacktestOverfitting,
    string rationale)
{
    public static IntegrityResponse From(BacktestIntegrityReport r) => new(
        r.Verdict.ToString(),
        r.Sharpe,
        r.AnnualizedSharpe,
        r.ProbabilisticSharpe.Value,
        r.DeflatedSharpe.Value,
        r.TStatistic,
        r.Skewness,
        r.Kurtosis,
        r.Observations,
        r.Trials,
        r.ProbabilityOfBacktestOverfitting?.Value,
        r.Rationale);
}
