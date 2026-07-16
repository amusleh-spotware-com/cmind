using Core;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes;

public sealed class BacktestCompletionPoller(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<BacktestCompletionPoller> log,
    TimeProvider timeProvider) : BackgroundService
{
    private const string ContainerExitedReason = "Container exited without producing a report";
    private const string BacktestTimedOutReason = "Backtest exceeded maximum allowed duration";
    // cTrader Console's own report writer throws "Message expected" and emits an empty report
    // (`"Equity":,`) when a backtest yields no equity curve — no trades OR no market data in the range.
    // We can't fix cTrader, so translate that raw crash into an actionable failure reason.
    private const string NoBacktestResultReason =
        "cTrader produced no backtest results for the selected range — no trades, or no market data for "
        + "those dates/symbol. Pick a wider date range that has available market data and retry.";

    // Chooses the failure reason for a backtest that exited without a readable report: the specific
    // no-result message when cTrader's report writer crashed on an empty report, else the generic one.
    // Pure so it is unit-tested without a container.
    internal static string DescribeMissingReport(string? consoleLog) =>
        !string.IsNullOrEmpty(consoleLog)
        && (consoleLog.Contains("Message expected", StringComparison.Ordinal)
            || consoleLog.Contains("\"Equity\":,", StringComparison.Ordinal))
            ? NoBacktestResultReason
            : ContainerExitedReason;

    // A running backtest is overdue once it has run longer than the configured maximum. Pure so the
    // timeout boundary is unit-tested without a DB or container.
    internal static bool IsOverdue(DateTimeOffset startedAt, DateTimeOffset now, TimeSpan maxDuration)
        => now - startedAt > maxDuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (Exception ex) { log.BacktestPollFailed(ex); }
            await Task.Delay(options.CurrentValue.BacktestCompletionPollInterval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IContainerDispatcherFactory>();

        var running = await db.Instances.OfType<RunningBacktestInstance>()
            .Include(i => i.Node)
            .ToListAsync(ct);

        var maxDuration = options.CurrentValue.MaxBacktestDuration;
        var now = timeProvider.GetUtcNow();
        foreach (var instance in running)
        {
            if (instance.Node is null) continue;

            // A hung backtest container never flips to exited, so the status check below would skip it
            // forever. Force-stop and fail it once it has outrun the max duration.
            if (IsOverdue(instance.StartedAt, now, maxDuration))
            {
                await CaptureConsoleLogAsync(factory, instance, ct);
                try { await factory.For(instance).StopAsync(instance, ct); }
                catch (Exception ex) { log.BacktestStatusCheckFailed(instance.Id.Value, ex); }
                log.BacktestTimedOut(instance.Id.Value);
                db.Instances.Remove(instance);
                db.Instances.Add(instance.ToFailed(BacktestTimedOutReason, now));
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
                continue;
            }

            bool? isRunning;
            try { isRunning = await factory.For(instance).IsRunningAsync(instance, ct); }
            catch (Exception ex) { log.BacktestStatusCheckFailed(instance.Id.Value, ex); continue; }
            if (isRunning != false) continue;

            // Capture the exited container's console output before transitioning, so the completed/failed
            // backtest's logs stay viewable and downloadable (parity with a run's persisted logs).
            await CaptureConsoleLogAsync(factory, instance, ct);

            var reportJson = await TryReadReportAsync(factory, instance, ct);
            Instance terminal = reportJson is not null
                ? instance.ToCompleted(now, reportJson, instance.DataDirSubPath)
                : instance.ToFailed(DescribeMissingReport(instance.ConsoleLog), now);
            db.Instances.Remove(instance);
            db.Instances.Add(terminal);

            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                // Instance was already transitioned elsewhere (e.g. the stop endpoint) - drop our change.
                db.ChangeTracker.Clear();
            }
        }
    }

    private static async Task<string?> TryReadReportAsync(
        IContainerDispatcherFactory factory, Instance instance, CancellationToken ct)
    {
        try { return await factory.For(instance).ReadReportAsync(instance, ct); }
        catch { return null; }
    }

    private const int MaxLogChars = 200_000;

    // Tails a bounded snapshot of the (exited) container's console output and stores it on the instance so it
    // survives the container being removed. Best-effort — never throws into the poll loop.
    private static async Task CaptureConsoleLogAsync(
        IContainerDispatcherFactory factory, Instance instance, CancellationToken ct)
    {
        var builder = new System.Text.StringBuilder();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await foreach (var line in factory.For(instance).TailLogsAsync(instance, cts.Token))
            {
                builder.AppendLine(line);
                if (builder.Length >= MaxLogChars) break;
            }
        }
        catch (OperationCanceledException) { /* budget reached */ }
        catch { /* logs unavailable */ }

        if (builder.Length > 0) instance.CaptureConsoleLog(builder.ToString());
    }

}
