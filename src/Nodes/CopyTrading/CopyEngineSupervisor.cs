using System.Collections.Concurrent;
using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using CopyEngine;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.CopyTrading;

/// <summary>
/// In-process copy hosting for the local node. Each cycle it reconciles running copy profiles with
/// live <see cref="CopyEngineHost"/> instances, starting hosts for newly running profiles and
/// cancelling those no longer running. Gated on <see cref="CopyOptions.Enabled"/> (off by default).
/// Order execution requires validation against a real authorised trading account.
/// </summary>
public sealed class CopyEngineSupervisor(
    IServiceScopeFactory scopeFactory,
    IOpenApiTradingSessionFactory sessionFactory,
    IOptionsMonitor<AppOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<CopyEngineSupervisor> log,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly ConcurrentDictionary<CopyProfileId, HostHandle> _running = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.Copy.Enabled)
            {
                try { await ReconcileAsync(stoppingToken); }
                catch (Exception ex) { log.CopySupervisorFailed(ex); }
            }

            await Task.Delay(options.CurrentValue.Copy.ReconcileInterval, stoppingToken);
        }

        foreach (var handle in _running.Values) handle.Cts.Cancel();
    }

    // S1 graceful lease release: on shutdown (SIGTERM / rolling update) release this node's copy-profile
    // leases so a survivor reclaims them on its very next cycle, instead of the profile being un-hosted for
    // up to LeaseTtl. Runs after ExecuteAsync has been signalled to stop.
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var handle in _running.Values) handle.Cts.Cancel();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var node = ResolveNode();
            var released = await ReleaseLeasesAsync(db, node, CancellationToken.None);
            if (released > 0) log.CopyLeasesReleased(node.Value, released);
        }
        catch (Exception ex)
        {
            log.CopySupervisorFailed(ex);
        }
    }

    // Clears this node's assignment/lease on every profile it holds. Atomic ExecuteUpdate, so a survivor's
    // ClaimProfilesAsync (which grabs unassigned/lapsed profiles) picks them up on its next cycle.
    internal static Task<int> ReleaseLeasesAsync(DataContext db, NodeIdentity node, CancellationToken ct)
        => db.CopyProfiles
            .Where(p => p.Status == CopyProfileStatus.Running && p.AssignedNode == node.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.AssignedNode, (string?)null)
                .SetProperty(p => p.LeaseExpiresAt, (DateTimeOffset?)null), ct);

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var node = ResolveNode();
        var now = timeProvider.GetUtcNow();
        var leaseTtl = options.CurrentValue.Copy.LeaseTtl;
        await ClaimProfilesAsync(db, node, now, leaseTtl, stoppingToken);
        await RenewLeasesAsync(db, node, now + leaseTtl, stoppingToken);

        var mine = await db.CopyProfiles.Include(p => p.Destinations)
            .Where(p => p.Status == CopyProfileStatus.Running && p.AssignedNode == node.Value)
            .ToListAsync(stoppingToken);
        var mineIds = mine.Select(p => p.Id).ToHashSet();

        foreach (var (id, handle) in _running.ToArray())
        {
            if (mineIds.Contains(id)) continue;
            handle.Cts.Cancel();
            _running.TryRemove(id, out _);
        }

        // Watchdog (M2): a host whose task has exited or faulted while its profile is still ours is wedged
        // or dead. Drop it so the hosting loop below restarts it with a fresh plan/token — one profile's
        // crash never stalls the others (per-profile isolation), and "just restart it" is automatic.
        foreach (var (id, handle) in _running.ToArray())
        {
            if (!IsHostDead(handle.Task, id, mineIds)) continue;
            handle.Cts.Cancel();
            if (_running.TryRemove(id, out _)) log.CopyHostRestarted(id.Value);
        }

        foreach (var profile in mine)
        {
            var plan = await BuildPlanAsync(db, protector, profile, stoppingToken);
            if (plan is null)
            {
                log.CopyProfileNotLinkable(profile.Id.Value);
                continue;
            }

            var signature = TokenSignature(plan);
            if (_running.TryGetValue(profile.Id, out var existing))
            {
                // A rotated access token invalidates the token the running host still holds. Push the new
                // token so the host re-authorises the affected accounts in place on the live socket
                // (graceful, no event-stream drop) instead of tearing the host down and restarting.
                if (existing.TokenSignature == signature) continue;
                existing.Host.PushTokenUpdate(PlanTokens(plan));
                _running[profile.Id] = existing with { TokenSignature = signature };
                log.CopyHostTokenRotated(profile.Id.Value);
                continue;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var host = new CopyEngineHost(plan, sessionFactory,
                new CopyDecisionEngine(new CopySizingCalculator()), timeProvider, loggerFactory.CreateLogger<CopyEngineHost>());
            var task = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
            _running[profile.Id] = new HostHandle(task, cts, host, signature);
            log.CopyProfileHosted(profile.Id.Value);
        }

        // Flatten-all routing (C8): a user-requested flatten is delivered to the running host, then cleared
        // so it fires exactly once. The host closes + locks every destination in place.
        var flattenedAny = false;
        foreach (var profile in mine)
        {
            if (profile.FlattenRequestedAt is null || !_running.TryGetValue(profile.Id, out var handle)) continue;
            if (!handle.Host.PushFlatten()) continue; // host shutting down — keep the request pending
            profile.ClearFlattenRequest();
            flattenedAny = true;
        }
        if (flattenedAny) await db.SaveChangesAsync(stoppingToken);
    }

    // Claim every running profile that is unassigned OR whose lease has lapsed (its node died).
    // ExecuteUpdate is atomic per row, so two supervisors racing never both claim the same profile
    // (no double-copy), and a crashed node's profiles are picked up once the lease expires (self-heal).
    internal static Task<int> ClaimProfilesAsync(
        DataContext db, NodeIdentity node, DateTimeOffset now, TimeSpan leaseTtl, CancellationToken ct)
        => db.CopyProfiles
            .Where(p => p.Status == CopyProfileStatus.Running
                && (p.AssignedNode == null || p.LeaseExpiresAt == null || p.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.AssignedNode, node.Value)
                .SetProperty(p => p.LeaseExpiresAt, now + leaseTtl), ct);

    // Renew the lease on the profiles this node hosts so a live node keeps its claim across cycles.
    internal static Task<int> RenewLeasesAsync(
        DataContext db, NodeIdentity node, DateTimeOffset leaseUntil, CancellationToken ct)
        => db.CopyProfiles
            .Where(p => p.Status == CopyProfileStatus.Running && p.AssignedNode == node.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LeaseExpiresAt, leaseUntil), ct);

    // A hosted profile's host is dead when its run task has completed (ran to completion or faulted) while
    // the profile is still assigned to this node. Pure so the watchdog decision is unit-tested without a DB.
    internal static bool IsHostDead(Task hostTask, CopyProfileId id, IReadOnlySet<CopyProfileId> mineIds)
        => mineIds.Contains(id) && hostTask.IsCompleted;

    private NodeIdentity ResolveNode()
    {
        var configured = options.CurrentValue.Copy.NodeName;
        return new NodeIdentity(string.IsNullOrWhiteSpace(configured) ? Environment.MachineName : configured);
    }

    internal static string TokenSignature(CopyProfilePlan plan)
        => string.Join('|',
            $"{plan.SourceAccessToken}:{plan.SourceTokenVersion}",
            string.Join(',', plan.Destinations.Select(d => $"{d.AccessToken}:{d.TokenVersion}")));

    private static IReadOnlyList<(long Ctid, string Token)> PlanTokens(CopyProfilePlan plan)
    {
        var tokens = new List<(long Ctid, string Token)> { (plan.SourceCtidTraderAccountId, plan.SourceAccessToken) };
        tokens.AddRange(plan.Destinations.Select(d => (d.CtidTraderAccountId, d.AccessToken)));
        return tokens;
    }

    private static async Task<CopyProfilePlan?> BuildPlanAsync(
        DataContext db, ISecretProtector protector, CopyProfile profile, CancellationToken ct)
    {
        var source = await db.TradingAccounts.FirstOrDefaultAsync(t => t.Id == profile.SourceAccountId, ct);
        if (source?.OpenApiAuthorizationId is null || source.CtidTraderAccountId is null) return null;

        var sourceAuth = await db.OpenApiAuthorizations
            .FirstOrDefaultAsync(a => a.Id == source.OpenApiAuthorizationId, ct);
        if (sourceAuth is null) return null;

        var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == sourceAuth.ApplicationId, ct);
        if (application is null) return null;

        var clientSecret = Decrypt(protector, application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret);
        var sourceToken = Decrypt(protector, sourceAuth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken);

        var destinations = new List<CopyDestinationPlan>();
        foreach (var destination in profile.Destinations)
        {
            var account = await db.TradingAccounts.FirstOrDefaultAsync(t => t.Id == destination.DestinationAccountId, ct);
            if (account?.OpenApiAuthorizationId is null || account.CtidTraderAccountId is null) continue;

            var auth = await db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == account.OpenApiAuthorizationId, ct);
            if (auth is null) continue;

            destinations.Add(new CopyDestinationPlan(
                account.CtidTraderAccountId.Value,
                Decrypt(protector, auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken),
                auth.TokenVersion,
                destination));
        }

        if (destinations.Count == 0) return null;

        return new CopyProfilePlan(profile.Id, source.IsLive, application.ClientId, clientSecret,
            source.CtidTraderAccountId.Value, sourceToken, sourceAuth.TokenVersion, destinations);
    }

    private static string Decrypt(ISecretProtector protector, byte[] ciphertext, string purpose)
        => Encoding.UTF8.GetString(protector.Unprotect(ciphertext, purpose));

    private sealed record HostHandle(Task Task, CancellationTokenSource Cts, CopyEngineHost Host, string TokenSignature);
}
