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
    IOpenApiConnectionFactory connectionFactory,
    IOptionsMonitor<AppOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<CopyEngineSupervisor> log) : BackgroundService
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

        var running = await db.CopyProfiles.Include(p => p.Destinations)
            .Where(p => p.Status == CopyProfileStatus.Running).ToListAsync(stoppingToken);
        var runningIds = running.Select(p => p.Id).ToHashSet();

        foreach (var (id, handle) in _running.ToArray())
        {
            if (runningIds.Contains(id)) continue;
            handle.Cts.Cancel();
            _running.TryRemove(id, out _);
        }

        foreach (var profile in running)
        {
            if (_running.ContainsKey(profile.Id)) continue;

            var plan = await BuildPlanAsync(db, protector, profile, stoppingToken);
            if (plan is null)
            {
                log.CopyProfileNotLinkable(profile.Id.Value);
                continue;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var host = new CopyEngineHost(plan, connectionFactory,
                new CopyDecisionEngine(new CopySizingCalculator()), loggerFactory.CreateLogger<CopyEngineHost>());
            var task = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
            _running[profile.Id] = new HostHandle(task, cts);
            log.CopyProfileHosted(profile.Id.Value);
        }
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
                destination));
        }

        if (destinations.Count == 0) return null;

        return new CopyProfilePlan(profile.Id, source.IsLive, application.ClientId, clientSecret,
            source.CtidTraderAccountId.Value, sourceToken, destinations);
    }

    private static string Decrypt(ISecretProtector protector, byte[] ciphertext, string purpose)
        => Encoding.UTF8.GetString(protector.Unprotect(ciphertext, purpose));

    private sealed record HostHandle(Task Task, CancellationTokenSource Cts);
}
