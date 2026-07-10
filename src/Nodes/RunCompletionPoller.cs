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

public sealed class RunCompletionPoller(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<RunCompletionPoller> log,
    TimeProvider timeProvider) : BackgroundService
{
    private const string ContainerExitedReason = "Container exited with non-zero code ";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (Exception ex) { log.RunPollFailed(ex); }
            await Task.Delay(options.CurrentValue.RunCompletionPollInterval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IContainerDispatcherFactory>();

        var running = await db.Instances.OfType<RunningRunInstance>()
            .Include(i => i.Node)
            .ToListAsync(ct);

        foreach (var instance in running)
        {
            if (instance.Node is null) continue;
            bool? isRunning;
            try { isRunning = await factory.For(instance).IsRunningAsync(instance, ct); }
            catch (Exception ex) { log.RunStatusCheckFailed(instance.Id.Value, ex); continue; }
            if (isRunning != false) continue;

            int? exitCode;
            try { exitCode = await factory.For(instance).GetExitCodeAsync(instance, ct); }
            catch { exitCode = null; }

            var now = timeProvider.GetUtcNow();
            Instance terminal = exitCode is null or 0
                ? instance.ToStopped(now)
                : instance.ToFailed($"{ContainerExitedReason}{exitCode}", now);
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
}
