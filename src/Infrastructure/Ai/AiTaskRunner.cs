using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

/// <summary>
/// Web-host background worker that runs asynchronous AI tasks so a user can start one, navigate away, and
/// return to its result. Each cycle it atomically claims one claimable task (Queued, or an orphaned Running
/// task whose lease expired) with a Postgres <c>FOR UPDATE SKIP LOCKED</c> lease so multiple replicas never
/// double-run one, dispatches it by feature (a "build cBot" task runs the shared <see cref="CBotBuildFlow"/>
/// on the task's chosen model), streams progress into the task's log, and records the terminal result. A
/// crash mid-run simply lets the lease expire so another cycle reclaims it. Runs on the web host because a
/// cBot build needs its Docker socket.
/// </summary>
public sealed class AiTaskRunner(
    IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    IOptionsMonitor<AppOptions> options, ILogger<AiTaskRunner> logger) : BackgroundService
{
    private readonly string _node = Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.AiTaskRunnerFailed(ex);
            }

            try
            {
                await Task.Delay(AiConstants.AiTaskPollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>Claim and run at most one task; returns true when a task was processed. Public so a test (or a
    /// manual trigger) can drive a single cycle deterministically.</summary>
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        // White-label/owner can disable the async task feature at runtime — stop claiming when off.
        if (!options.CurrentValue.Branding.AllowAiTasks) return false;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var task = await ClaimAsync(db, ct);
        if (task is null) return false;

        await RunAsync(scope.ServiceProvider, db, task, ct);
        logger.AiTaskFinished(task.Id.Value, task.Status.ToString());
        return true;
    }

    // Atomically pick + claim the oldest claimable task under a row lock so concurrent web replicas can't
    // both run it. FOR UPDATE SKIP LOCKED hands each replica a different row (or none).
    private async Task<AiTask?> ClaimAsync(DataContext db, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        const string sql = """
            SELECT * FROM "AiTasks"
            WHERE "IsDeleted" = false
              AND ("Status" = 'Queued'
                   OR ("Status" = 'Running' AND "LeaseExpiresAt" IS NOT NULL AND "LeaseExpiresAt" <= {0}))
            ORDER BY "CreatedAt"
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """;

        var task = await db.AiTasks.FromSqlRaw(sql, now).FirstOrDefaultAsync(ct);
        if (task is null)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        task.Claim(_node, now, AiConstants.AiTaskLeaseTtl);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return task;
    }

    private async Task RunAsync(IServiceProvider services, DataContext db, AiTask task, CancellationToken ct)
    {
        try
        {
            switch (task.Feature)
            {
                case AiFeature.GenerateCBot:
                    await RunBuildAsync(services, db, task, ct);
                    break;
                default:
                    task.Fail("This AI feature cannot run as a background task yet.", timeProvider.GetUtcNow());
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            task.Log($"Task errored: {ex.Message}", timeProvider.GetUtcNow());
            if (task.IsActive) task.Fail("The task errored while running.", timeProvider.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RunBuildAsync(IServiceProvider services, DataContext db, AiTask task, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<AiTaskBuildPayload>(task.PayloadJson)
                      ?? new AiTaskBuildPayload(null, "CSharp", null);
        var flow = services.GetRequiredService<CBotBuildFlow>();

        var result = await flow.BuildAsync(
            task.UserId, payload.Name ?? "AiBot", payload.Language ?? "CSharp", payload.Description ?? "",
            task.CredentialId, line => task.Log(line, timeProvider.GetUtcNow()), ct);

        var now = timeProvider.GetUtcNow();
        if (result.Success)
        {
            var refs = JsonSerializer.Serialize(new { cBotId = result.CBotId, projectId = result.ProjectId });
            task.Succeed(result.Code ?? string.Empty, refs, now);
        }
        else
        {
            task.Fail(result.Error ?? $"Build did not succeed after {result.Attempts} attempt(s).", now);
        }
    }
}
