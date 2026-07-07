using Core;
using Core.Ai;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes;

public sealed class AiRiskGuard(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<AiRiskGuard> logger) : BackgroundService
{
    private const int MaxSummaryChars = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(options.CurrentValue.Ai.RiskGuardInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.RiskGuardFailed(ex); }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        if (!options.CurrentValue.Ai.RiskGuardEnabled) return;

        using var scope = scopeFactory.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<IAiFeatureService>();
        if (!ai.Enabled) return;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var running = await db.Instances.OfType<RunningRunInstance>()
            .Select(i => new AiInstanceContext(i.CBot.Name, "Run", "Running", i.Symbol, i.Timeframe, null))
            .ToListAsync(ct);
        if (running.Count == 0) return;

        var result = await ai.AssessRiskAsync(running, ct);
        if (result.Success)
            logger.RiskGuardAssessment(running.Count, Truncate(result.Text, MaxSummaryChars));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
