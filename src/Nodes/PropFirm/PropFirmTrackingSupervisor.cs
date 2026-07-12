using System.Collections.Concurrent;
using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using Core.PropFirm;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.PropFirm;

/// <summary>
/// In-process prop-firm challenge tracking for a node. Each cycle it claims active challenges on a
/// self-healing lease (a dead node's challenges are reclaimed once the lease lapses), starts a
/// <see cref="PropFirmTrackingHost"/> per claimed challenge, renews leases, pushes rotated tokens in
/// place, and stops hosts whose challenge left <see cref="ChallengeStatus.Active"/>. Gated on
/// <see cref="PropFirmOptions.Enabled"/> (off by default) — live tracking needs a real authorised account.
/// </summary>
public sealed class PropFirmTrackingSupervisor(
    IServiceScopeFactory scopeFactory,
    IOpenApiTradingSessionFactory sessionFactory,
    IOptionsMonitor<AppOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<PropFirmTrackingSupervisor> log,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly ConcurrentDictionary<PropFirmChallengeId, HostHandle> _running = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.PropFirm.Enabled)
            {
                try { await ReconcileAsync(stoppingToken); }
                catch (Exception ex) { log.PropFirmSupervisorFailed(ex); }
            }

            await Task.Delay(options.CurrentValue.PropFirm.ReconcileInterval, stoppingToken);
        }

        await Task.WhenAll(_running.Values.Select(h => h.Cts.CancelAsync()));
    }

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var node = ResolveNode();
        var now = timeProvider.GetUtcNow();
        var settings = options.CurrentValue.PropFirm;
        await ClaimChallengesAsync(db, node, now, settings.LeaseTtl, stoppingToken);
        await RenewLeasesAsync(db, node, now + settings.LeaseTtl, stoppingToken);

        var mine = await db.PropFirmChallenges
            .Where(c => c.Status == ChallengeStatus.Active && c.AssignedNode == node.Value)
            .ToListAsync(stoppingToken);
        var mineIds = mine.Select(c => c.Id).ToHashSet();

        foreach (var (id, handle) in _running.ToArray())
        {
            if (mineIds.Contains(id)) continue;
            await handle.Cts.CancelAsync();
            _running.TryRemove(id, out _);
        }

        foreach (var challenge in mine)
        {
            var plan = await BuildPlanAsync(db, protector, challenge, stoppingToken);
            if (plan is null)
            {
                log.PropFirmChallengeNotTrackable(challenge.Id.Value);
                continue;
            }

            var signature = TokenSignature(plan);
            if (_running.TryGetValue(challenge.Id, out var existing))
            {
                if (existing.TokenSignature == signature) continue;
                await existing.Host.PushTokenUpdateAsync(plan.AccessToken, stoppingToken);
                _running[challenge.Id] = existing with { TokenSignature = signature };
                continue;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var host = new PropFirmTrackingHost(plan, sessionFactory, new PropFirmEquityCalculator(),
                scopeFactory, timeProvider, settings.EquityPollInterval, settings.DrawdownWarnThresholdPercent,
                loggerFactory.CreateLogger<PropFirmTrackingHost>());
            var task = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
            _running[challenge.Id] = new HostHandle(task, cts, host, signature);
            log.PropFirmChallengeHosted(challenge.Id.Value);
        }
    }

    // Claim every active challenge that is unassigned OR whose lease has lapsed (its node died).
    // ExecuteUpdate is atomic per row, so two supervisors racing never both claim the same challenge
    // (no double-tracking), and a crashed node's challenges are reclaimed once the lease expires.
    internal static Task<int> ClaimChallengesAsync(
        DataContext db, NodeIdentity node, DateTimeOffset now, TimeSpan leaseTtl, CancellationToken ct)
        => db.PropFirmChallenges
            .Where(c => c.Status == ChallengeStatus.Active
                && (c.AssignedNode == null || c.LeaseExpiresAt == null || c.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AssignedNode, node.Value)
                .SetProperty(c => c.LeaseExpiresAt, now + leaseTtl), ct);

    internal static Task<int> RenewLeasesAsync(
        DataContext db, NodeIdentity node, DateTimeOffset leaseUntil, CancellationToken ct)
        => db.PropFirmChallenges
            .Where(c => c.Status == ChallengeStatus.Active && c.AssignedNode == node.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LeaseExpiresAt, leaseUntil), ct);

    private NodeIdentity ResolveNode()
    {
        var configured = options.CurrentValue.PropFirm.NodeName;
        return new NodeIdentity(string.IsNullOrWhiteSpace(configured) ? Environment.MachineName : configured);
    }

    internal static string TokenSignature(PropFirmTrackingPlan plan) => $"{plan.AccessToken}:{plan.TokenVersion}";

    private static async Task<PropFirmTrackingPlan?> BuildPlanAsync(
        DataContext db, ISecretProtector protector, PropFirmChallenge challenge, CancellationToken ct)
    {
        var account = await db.TradingAccounts.FirstOrDefaultAsync(t => t.Id == challenge.TradingAccountId, ct);
        if (account?.OpenApiAuthorizationId is null || account.CtidTraderAccountId is null) return null;

        var auth = await db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == account.OpenApiAuthorizationId, ct);
        if (auth is null) return null;

        var application = await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == auth.ApplicationId, ct);
        if (application is null) return null;

        var clientSecret = Decrypt(protector, application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret);
        var accessToken = Decrypt(protector, auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken);

        return new PropFirmTrackingPlan(challenge.Id, challenge.UserId, account.IsLive, application.ClientId,
            clientSecret, account.CtidTraderAccountId.Value, accessToken, auth.TokenVersion);
    }

    private static string Decrypt(ISecretProtector protector, byte[] ciphertext, string purpose)
        => Encoding.UTF8.GetString(protector.Unprotect(ciphertext, purpose));

    private sealed record HostHandle(Task Task, CancellationTokenSource Cts, PropFirmTrackingHost Host, string TokenSignature);
}
