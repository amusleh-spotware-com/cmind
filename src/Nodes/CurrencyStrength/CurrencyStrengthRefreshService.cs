using Core.Logging;
using Core.Options;
using Infrastructure.Ai.CurrencyStrength;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.CurrencyStrength;

/// <summary>
/// Config-gated scheduled refresh of the macro currency-strength snapshot. Off unless
/// <c>App:CurrencyStrength:RefreshEnabled</c> is set (the read side, explicit owner refresh and degradation
/// paths all work without it). Orchestration only — the deterministic decisions live in the domain
/// calculators, driven by <see cref="CurrencyStrengthRefresher"/>. Uses the injected <see cref="TimeProvider"/>,
/// never the wall clock, and degrades to a logged failure rather than crashing the host.
/// </summary>
public sealed class CurrencyStrengthRefreshService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider,
    ILogger<CurrencyStrengthRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.CurrencyStrength.RefreshEnabled) return;

        var interval = options.CurrentValue.CurrencyStrength.RefreshInterval;
        using var timer = new PeriodicTimer(interval, timeProvider);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var refresher = scope.ServiceProvider.GetRequiredService<CurrencyStrengthRefresher>();
                await refresher.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogMessages.CurrencyStrengthRefreshCycleFailed(logger, ex);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
