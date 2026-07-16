using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record StartRequest(
    Guid CBotId, Guid TradingAccountId, string Symbol, string Timeframe,
    Guid? ParamSetId, string DockerImageTag, string Type,
    string? BacktestSettingsJson);

// Re-launch a stopped instance with a CHANGED configuration (same cBot + kind). Backtest window fields
// are ignored for a run.
public record EditInstanceRequest(
    Guid? TradingAccountId, string Symbol, string Timeframe,
    Guid? ParamSetId, string DockerImageTag, string? BacktestSettingsJson);

public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapInstanceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/instances").RequireAuthorization()
            .RequireFeature(Core.Features.FeatureFlag.Execution);

        g.MapGet("/", async (DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            IQueryable<Instance> q = db.Instances.AsNoTracking().Include(i => i.CBot).Include(i => i.Node);
            if (u.IsInRole("Viewer"))
            {
                var user = await db.Users.OfType<ViewerUser>().FirstOrDefaultAsync(x => x.Id == uid);
                if (user is null) return Results.Unauthorized();
                if (!user.SeeAllInstances)
                {
                    var grants = db.ViewerGrants.Where(v => v.ViewerId == uid).Select(v => v.InstanceId);
                    q = q.Where(i => grants.Contains(i.Id));
                }
            }
            else if (u.IsInRole("User"))
            {
                q = q.Where(i => i.UserId == uid);
            }
            var instances = await q.OrderByDescending(i => i.CreatedAt).Take(200).ToListAsync();
            var rows = instances.Select(i => new
            {
                i.Id,
                Kind = i.KindName,
                Status = i.StatusName,
                i.Symbol,
                i.Timeframe,
                CBot = i.CBot.Name,
                Node = i.Node!.Name,
                StartedAt = GetStartedAt(i),
                StoppedAt = GetStoppedAt(i),
                // Logs are downloadable when they were persisted at termination, or the instance is still
                // live on a node (a snapshot can be tailed on demand).
                HasLogs = i.ConsoleLog is not null || (i.IsActive && i.NodeId is not null)
            });
            return Results.Ok(rows);
        });

        g.MapGet("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.AsNoTracking().Include(x => x.CBot).FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer"))
            {
                var viewer = await db.Users.OfType<ViewerUser>().FirstOrDefaultAsync(x => x.Id == uid);
                if (viewer is null) return Results.Unauthorized();
                if (!viewer.SeeAllInstances && !await db.ViewerGrants.AnyAsync(v => v.ViewerId == uid && v.InstanceId == iid))
                    return Results.Forbid();
            }
            else if (u.IsInRole("User") && i.UserId != uid)
            {
                return Results.Forbid();
            }

            return Results.Ok(BuildDetail(i));
        });

        // Resolve the CURRENT state of an instance lineage. A TPH transition replaces the entity (its id
        // changes) but carries LineageId across, so polling by the unique LineageId follows exactly one
        // instance across Starting → Running → Stopped/Completed/Failed — a run and a backtest of the same
        // cBot are distinct lineages and can never be confused.
        g.MapGet("/current", async (Guid lineageId, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var lid = InstanceLineageId.From(lineageId);
            var i = await db.Instances.AsNoTracking().Include(x => x.CBot).FirstOrDefaultAsync(x => x.LineageId == lid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer"))
            {
                var viewer = await db.Users.OfType<ViewerUser>().FirstOrDefaultAsync(x => x.Id == uid);
                if (viewer is null) return Results.Unauthorized();
                if (!viewer.SeeAllInstances && !await db.ViewerGrants.AnyAsync(v => v.ViewerId == uid && v.InstanceId == i.Id))
                    return Results.Forbid();
            }
            else if (u.IsInRole("User") && i.UserId != uid)
            {
                return Results.Forbid();
            }
            return Results.Ok(BuildDetail(i));
        });

        g.MapPost("/", async (StartRequest req, DataContext db, ICurrentUser u,
            INodeScheduler scheduler, IContainerDispatcherFactory factory, ISecretProtector protector,
            TimeProvider timeProvider) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (u.IsInRole("Viewer")) return Results.Forbid();

            var cbotId = CBotId.From(req.CBotId);
            var accountId = TradingAccountId.From(req.TradingAccountId);

            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == cbotId && c.UserId == uid);
            if (cbot is null) return Results.BadRequest("cbot not found");
            var acct = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.Id == accountId && t.CTid.UserId == uid);
            if (acct is null) return Results.BadRequest("account not found");

            // A parameter set is optional: none ⇒ run with the cBot's default parameter values (empty
            // cbotset). When supplied it must belong to the caller.
            ParamSetId? paramSetId = null;
            var paramJson = "{}";
            if (req.ParamSetId is { } psid)
            {
                var paramSet = await db.ParamSets.FirstOrDefaultAsync(p => p.Id == ParamSetId.From(psid) && p.UserId == uid);
                if (paramSet is null) return Results.BadRequest("paramset not found");
                paramSetId = paramSet.Id;
                paramJson = paramSet.JsonContent;
            }

            var kind = req.Type;
            var node = await scheduler.PickNodeAsync(kind, default);
            if (node is null) return Results.Conflict("no node available");

            var imageTag = string.IsNullOrWhiteSpace(req.DockerImageTag) ? "latest" : req.DockerImageTag;

            var imageTagValue = new DockerImageTag(imageTag);
            var symbol = new Symbol(req.Symbol);
            var timeframe = new Timeframe(req.Timeframe);
            Instance starting = string.Equals(kind, "Backtest", StringComparison.OrdinalIgnoreCase)
                ? BacktestInstance.CreateStarting(uid, cbotId, node.Id, imageTagValue, symbol, timeframe,
                    req.BacktestSettingsJson, accountId, paramSetId)
                : RunInstance.CreateStarting(uid, cbotId, node.Id, imageTagValue, symbol, timeframe,
                    accountId, paramSetId);
            db.Instances.Add(starting);
            await db.SaveChangesAsync();

            // Re-load node for dispatcher context
            starting.AttachNode(node);

            var algo = protector.Unprotect(cbot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);

            string containerId;
            try
            {
                containerId = await factory.For(node).StartAsync(starting, algo, paramJson, default);
            }
            catch (Exception ex)
            {
                // Container start failed (e.g. image pull / docker error): record the instance as Failed and
                // still return OK so the caller shows the (failed) instance in the list instead of a 500 with
                // an orphaned Starting row.
                Instance failedInstance = starting is BacktestInstance failedBacktest
                    ? failedBacktest.ToFailed(ex.Message, timeProvider.GetUtcNow())
                    : ((RunInstance)starting).ToFailed(ex.Message, timeProvider.GetUtcNow());
                db.Instances.Remove(starting);
                db.Instances.Add(failedInstance);
                await db.SaveChangesAsync();
                return Results.Ok(new { failedInstance.Id });
            }

            // Transition to Running by replacing entity (TPH discriminator cannot change)
            Instance running = starting switch
            {
                StartingBacktestInstance sb => sb.ToRunning(containerId, timeProvider.GetUtcNow()),
                StartingRunInstance sr => sr.ToRunning(containerId, timeProvider.GetUtcNow()),
                _ => throw new InvalidOperationException()
            };
            db.Instances.Remove(starting);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { running.Id });
        });

        g.MapPost("/{id:guid}/stop", async (Guid id, DataContext db, ICurrentUser u,
            IContainerDispatcherFactory factory, TimeProvider timeProvider) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer") || (u.IsInRole("User") && i.UserId != uid))
                return Results.Forbid();

            if (i.Node is not null)
            {
                var dispatcher = factory.For(i);
                // Capture the console output BEFORE stopping — StopAsync removes the container, taking its
                // logs with it. Persist a bounded snapshot so the last run's logs stay downloadable.
                var captured = await CaptureLogsAsync(dispatcher, i);
                if (!string.IsNullOrWhiteSpace(captured)) i.CaptureConsoleLog(captured);
                try { await dispatcher.StopAsync(i, default); } catch { /* swallow */ }
            }

            // Replace with Stopped/Completed entity
            var now = timeProvider.GetUtcNow();
            Instance terminal;
            if (i is RunningRunInstance rri) terminal = rri.ToStopped(now);
            else if (i is RunningBacktestInstance rbi) terminal = rbi.ToCompleted(now);
            else return Results.Ok();
            db.Instances.Remove(i);
            db.Instances.Add(terminal);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Restart a terminal instance (run or backtest): launches a fresh instance of the same kind with the
        // same cBot / account / symbol / timeframe / parameter set / image (and backtest settings) as the
        // stopped/completed/failed one.
        g.MapPost("/{id:guid}/start", async (Guid id, DataContext db, ICurrentUser u,
            INodeScheduler scheduler, IContainerDispatcherFactory factory, ISecretProtector protector,
            TimeProvider timeProvider) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (u.IsInRole("Viewer")) return Results.Forbid();
            var iid = InstanceId.From(id);
            var i = await db.Instances.FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("User") && i.UserId != uid) return Results.Forbid();
            if (!i.IsTerminal) return Results.Conflict("instance is not stopped");

            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == i.CBotId && c.UserId == uid);
            if (cbot is null) return Results.BadRequest("cbot not found");

            // Load the trading account (with its cID) into the context so EF relationship fixup populates the
            // new instance's TradingAccount navigation — the dispatcher needs it to pass --ctid/--pwd-file/
            // --account to the cTrader CLI (otherwise the container errors "Should be specified parameter:
            // --pwd-file").
            if (i.TradingAccountId is { } accountId)
                await db.TradingAccounts.Include(t => t.CTid).FirstOrDefaultAsync(t => t.Id == accountId);

            var paramJson = "{}";
            if (i.ParamSetId is { } psid)
            {
                var paramSet = await db.ParamSets.FirstOrDefaultAsync(p => p.Id == psid && p.UserId == uid);
                if (paramSet is not null) paramJson = paramSet.JsonContent;
            }

            var kind = i is BacktestInstance ? "Backtest" : "Run";
            var node = await scheduler.PickNodeAsync(kind, default);
            if (node is null) return Results.Conflict("no node available");

            var imageTag = new DockerImageTag(i.DockerImageTag);
            var symbol = new Symbol(i.Symbol ?? "EURUSD");
            var timeframe = new Timeframe(i.Timeframe ?? "h1");
            Instance starting = i is BacktestInstance backtest
                ? BacktestInstance.CreateStarting(uid, i.CBotId, node.Id, imageTag, symbol, timeframe,
                    backtest.BacktestSettingsJson, i.TradingAccountId, i.ParamSetId)
                : RunInstance.CreateStarting(uid, i.CBotId, node.Id, imageTag, symbol, timeframe,
                    i.TradingAccountId, i.ParamSetId);
            // Replace the old terminal instance rather than leaving a duplicate — a restart re-launches "the
            // same" instance in the list.
            db.Instances.Remove(i);
            db.Instances.Add(starting);
            await db.SaveChangesAsync();
            starting.AttachNode(node);

            var algo = protector.Unprotect(cbot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);
            string containerId;
            try
            {
                containerId = await factory.For(node).StartAsync(starting, algo, paramJson, default);
            }
            catch (Exception ex)
            {
                Instance failedInstance = starting is BacktestInstance failedBacktest
                    ? failedBacktest.ToFailed(ex.Message, timeProvider.GetUtcNow())
                    : ((RunInstance)starting).ToFailed(ex.Message, timeProvider.GetUtcNow());
                db.Instances.Remove(starting);
                db.Instances.Add(failedInstance);
                await db.SaveChangesAsync();
                return Results.Ok(new { failedInstance.Id });
            }

            Instance running = starting switch
            {
                StartingBacktestInstance sb => sb.ToRunning(containerId, timeProvider.GetUtcNow()),
                StartingRunInstance sr => sr.ToRunning(containerId, timeProvider.GetUtcNow()),
                _ => throw new InvalidOperationException()
            };
            db.Instances.Remove(starting);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { running.Id });
        });

        // Re-launch a terminal instance (run or backtest) with a CHANGED configuration: a new account,
        // symbol, timeframe, parameter set, image tag, and (backtest) window. Same cBot and kind. Replaces
        // the old terminal instance, mirroring the restart endpoint but using the caller's overrides.
        g.MapPost("/{id:guid}/edit", async (Guid id, EditInstanceRequest req, DataContext db, ICurrentUser u,
            INodeScheduler scheduler, IContainerDispatcherFactory factory, ISecretProtector protector,
            TimeProvider timeProvider) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (u.IsInRole("Viewer")) return Results.Forbid();
            var iid = InstanceId.From(id);
            var i = await db.Instances.FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("User") && i.UserId != uid) return Results.Forbid();
            if (!i.IsTerminal) return Results.Conflict("instance is not stopped");
            if (string.IsNullOrWhiteSpace(req.Symbol) || string.IsNullOrWhiteSpace(req.Timeframe))
                return Results.BadRequest("symbol and timeframe are required");

            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == i.CBotId && c.UserId == uid);
            if (cbot is null) return Results.BadRequest("cbot not found");

            // Resolve the (changed) trading account — loaded with its cID so EF fixup populates the new
            // instance's TradingAccount navigation for --ctid/--pwd-file/--account.
            TradingAccountId? accountId = null;
            if (req.TradingAccountId is { } aid)
            {
                var acct = await db.TradingAccounts.Include(t => t.CTid)
                    .FirstOrDefaultAsync(t => t.Id == TradingAccountId.From(aid) && t.CTid.UserId == uid);
                if (acct is null) return Results.BadRequest("account not found");
                accountId = acct.Id;
            }

            ParamSetId? paramSetId = null;
            var paramJson = "{}";
            if (req.ParamSetId is { } psid)
            {
                var paramSet = await db.ParamSets.FirstOrDefaultAsync(p => p.Id == ParamSetId.From(psid) && p.UserId == uid);
                if (paramSet is null) return Results.BadRequest("paramset not found");
                paramSetId = paramSet.Id;
                paramJson = paramSet.JsonContent;
            }

            var kind = i is BacktestInstance ? "Backtest" : "Run";
            var node = await scheduler.PickNodeAsync(kind, default);
            if (node is null) return Results.Conflict("no node available");

            var imageTag = new DockerImageTag(string.IsNullOrWhiteSpace(req.DockerImageTag) ? "latest" : req.DockerImageTag);
            var symbol = new Symbol(req.Symbol);
            var timeframe = new Timeframe(req.Timeframe);
            Instance starting = i is BacktestInstance
                ? BacktestInstance.CreateStarting(uid, i.CBotId, node.Id, imageTag, symbol, timeframe,
                    req.BacktestSettingsJson, accountId, paramSetId)
                : RunInstance.CreateStarting(uid, i.CBotId, node.Id, imageTag, symbol, timeframe,
                    accountId, paramSetId);
            db.Instances.Remove(i);
            db.Instances.Add(starting);
            await db.SaveChangesAsync();
            starting.AttachNode(node);

            var algo = protector.Unprotect(cbot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);
            string containerId;
            try
            {
                containerId = await factory.For(node).StartAsync(starting, algo, paramJson, default);
            }
            catch (Exception ex)
            {
                Instance failedInstance = starting is BacktestInstance failedBacktest
                    ? failedBacktest.ToFailed(ex.Message, timeProvider.GetUtcNow())
                    : ((RunInstance)starting).ToFailed(ex.Message, timeProvider.GetUtcNow());
                db.Instances.Remove(starting);
                db.Instances.Add(failedInstance);
                await db.SaveChangesAsync();
                return Results.Ok(new { failedInstance.Id });
            }

            Instance running = starting switch
            {
                StartingBacktestInstance sb => sb.ToRunning(containerId, timeProvider.GetUtcNow()),
                StartingRunInstance sr => sr.ToRunning(containerId, timeProvider.GetUtcNow()),
                _ => throw new InvalidOperationException()
            };
            db.Instances.Remove(starting);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { running.Id });
        });

        g.MapGet("/{id:guid}/logs", async (Guid id, DataContext db, ICurrentUser u,
            IContainerDispatcherFactory factory) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.Include(x => x.Node).FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer"))
            {
                var viewer = await db.Users.OfType<ViewerUser>().FirstOrDefaultAsync(x => x.Id == uid);
                if (viewer is null) return Results.Unauthorized();
                if (!viewer.SeeAllInstances && !await db.ViewerGrants.AnyAsync(v => v.ViewerId == uid && v.InstanceId == iid))
                    return Results.Forbid();
            }
            else if (u.IsInRole("User") && i.UserId != uid)
            {
                return Results.Forbid();
            }

            // Persisted logs win; for a still-live instance, tail a bounded snapshot on demand.
            var text = i.ConsoleLog;
            if (string.IsNullOrEmpty(text) && i.IsActive && i.Node is not null)
                text = await CaptureLogsAsync(factory.For(i), i);
            if (string.IsNullOrEmpty(text)) return Results.NotFound("no logs available");

            return Results.File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain", $"instance-{id:N}.log");
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var i = await db.Instances.FirstOrDefaultAsync(x => x.Id == iid);
            if (i is null) return Results.NotFound();
            if (u.IsInRole("Viewer") || (u.IsInRole("User") && i.UserId != uid))
                return Results.Forbid();
            if (i.IsActive) return Results.Conflict("stop first");
            db.Instances.Remove(i);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static object BuildDetail(Instance i)
    {
        var equity = i is CompletedBacktestInstance completed
            ? ContainerCommandHelpers.ParseEquityCurve(completed.ReportJson).Select(p => new { p.Timestamp, p.Value })
            : null;
        return new
        {
            i.Id,
            Kind = i.KindName,
            Status = i.StatusName,
            i.Symbol,
            i.Timeframe,
            CbotName = i.CBot?.Name,
            // Editable config for the Edit-instance dialog to prefill (a stopped instance can be re-launched
            // with a changed account / symbol / timeframe / parameter set / image / backtest window).
            CbotId = i.CBotId,
            TradingAccountId = i.TradingAccountId,
            ParamSetId = i.ParamSetId,
            i.DockerImageTag,
            BacktestSettingsJson = i is BacktestInstance bt ? bt.BacktestSettingsJson : null,
            Equity = equity,
            i.LineageId
        };
    }

    private const int MaxLogChars = 200_000;

    // Tails a bounded snapshot of a container's console output (the docker `logs -f` backlog dumps first,
    // then the follow blocks) so a running or about-to-stop instance yields its logs within a short budget.
    private static async Task<string> CaptureLogsAsync(IContainerDispatcher dispatcher, Instance instance)
    {
        var builder = new System.Text.StringBuilder();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await foreach (var line in dispatcher.TailLogsAsync(instance, cts.Token))
            {
                builder.AppendLine(line);
                if (builder.Length >= MaxLogChars) break;
            }
        }
        catch (OperationCanceledException) { /* budget reached — return what was captured */ }
        catch { /* logs unavailable — return what was captured */ }
        return builder.ToString();
    }

    internal static DateTimeOffset? GetStartedAt(Instance i) => i switch
    {
        RunningRunInstance r => r.StartedAt,
        RunningBacktestInstance r => r.StartedAt,
        StoppingRunInstance r => r.StartedAt,
        StoppingBacktestInstance r => r.StartedAt,
        StoppedRunInstance r => r.StartedAt,
        CompletedBacktestInstance r => r.StartedAt,
        FailedRunInstance r => r.StartedAt,
        FailedBacktestInstance r => r.StartedAt,
        _ => null
    };

    internal static DateTimeOffset? GetStoppedAt(Instance i) => i switch
    {
        StoppedRunInstance r => r.StoppedAt,
        CompletedBacktestInstance r => r.StoppedAt,
        FailedRunInstance r => r.StoppedAt,
        FailedBacktestInstance r => r.StoppedAt,
        _ => null
    };
}
