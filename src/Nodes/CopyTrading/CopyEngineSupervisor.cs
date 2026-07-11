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
