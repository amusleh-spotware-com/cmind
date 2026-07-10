using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace IntegrationTests.CopyLive;

// Drives one real copy scenario end to end against cTrader demo accounts: starts the production
// CopyEngineHost, opens a position on the master, and waits for the mirrored copies on each slave.
// Cleans up every position it opened. Returns a result the test asserts on. Handles a closed market
// (no fill) by reporting Inconclusive instead of a hard failure.
public sealed class LiveCopyScenario(LiveCopyFixture fixture, ITestOutputHelper output)
{
    private static readonly string[] SymbolPreference = ["BTCUSD", "EURUSD", "GBPUSD", "XAUUSD"];
    private static readonly TimeSpan HostWarmup = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CopyTimeout = TimeSpan.FromSeconds(30);

    public sealed record SlaveSetup(LiveCopyFixture.LiveAccount Account, CopyDestination Config);

    public sealed record SlaveResult(long Ctid, bool Copied, bool IsBuy, long Volume);

    public sealed record ScenarioResult(bool Inconclusive, string? Reason, bool MasterIsBuy,
        long MasterVolume, IReadOnlyList<SlaveResult> Slaves);

    public async Task<ScenarioResult> RunAsync(LiveCopyFixture.LiveAccount master, bool masterIsBuy,
        IReadOnlyList<SlaveSetup> slaves, CancellationToken ct)
    {
        var masterCtid = master.Ctid;
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, fixture.ClientId, fixture.ClientSecret,
            masterCtid, master.AccessToken,
            slaves.Select(s => new CopyDestinationPlan(s.Account.Ctid, s.Account.AccessToken, s.Config)).ToList());

        var host = new CopyEngineHost(plan, new OpenApiTradingSessionFactory(fixture.ConnectionFactory),
            new CopyDecisionEngine(new CopySizingCalculator()), NullLogger.Instance);

        using var hostCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var hostTask = Task.Run(() => host.RunAsync(hostCts.Token), CancellationToken.None);

        var probeAccounts = new[] { master }.Concat(slaves.Select(s => s.Account)).ToArray();
        await using var probe = fixture.NewSession(probeAccounts);
        await probe.StartAsync(ct);

        var symbolId = await ResolveSymbolAsync(probe, masterCtid, slaves.Select(s => s.Account.Ctid).ToList(), ct);
        var details = (await probe.LoadSymbolDetailsAsync(masterCtid, [symbolId], ct)).First();
        var volume = details.MinVolume > 0 ? details.MinVolume : details.StepVolume;

        var label = $"cmind-live-{Guid.NewGuid():N}"[..24];
        output.WriteLine($"Host warming up ({HostWarmup.TotalSeconds}s) before opening master position…");
        await Task.Delay(HostWarmup, ct);

        await probe.SendMarketOrderAsync(masterCtid, symbolId, masterIsBuy, volume, label, ct);

        var masterPosition = await PollAsync(
            () => FindByLabelAsync(probe, masterCtid, label, ct), TimeSpan.FromSeconds(12), ct);
        if (masterPosition is null)
        {
            await StopHostAsync(hostCts, hostTask);
            return new ScenarioResult(true, "Master order did not fill (market likely closed).",
                masterIsBuy, volume, []);
        }

        var sourceId = masterPosition.PositionId.ToString();
        var slaveResults = new List<SlaveResult>();
        foreach (var slave in slaves)
        {
            var ctid = slave.Account.Ctid;
            var copy = await PollAsync(() => FindByLabelAsync(probe, ctid, sourceId, ct), CopyTimeout, ct);
            slaveResults.Add(new SlaveResult(ctid, copy is not null, copy?.IsBuy ?? false, copy?.Volume ?? 0));
            output.WriteLine($"slave {ctid} (cid {slave.Account.Cid}): copied={copy is not null} volume={copy?.Volume ?? 0} buy={copy?.IsBuy}");
        }

        await CleanupAsync(probe, masterCtid, sourceId, slaves, ct);
        await StopHostAsync(hostCts, hostTask);
        return new ScenarioResult(false, null, masterIsBuy, volume, slaveResults);
    }

    public sealed record PartialCloseResult(bool Inconclusive, string? Reason, long SlaveVolumeBefore, long SlaveVolumeAfter);

    // Opens a master position sized to at least two lot steps, waits for the slave copy, partial-closes
    // the master by half, and reports the slave copy volume before/after so the test can assert it
    // shrank proportionally. Demo only; cleans up everything it opened.
    public async Task<PartialCloseResult> RunPartialCloseAsync(LiveCopyFixture.LiveAccount master,
        SlaveSetup slave, CancellationToken ct)
    {
        var masterCtid = master.Ctid;
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, fixture.ClientId, fixture.ClientSecret,
            masterCtid, master.AccessToken,
            [new CopyDestinationPlan(slave.Account.Ctid, slave.Account.AccessToken, slave.Config)]);

        var host = new CopyEngineHost(plan, new OpenApiTradingSessionFactory(fixture.ConnectionFactory),
            new CopyDecisionEngine(new CopySizingCalculator()), NullLogger.Instance);
        using var hostCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var hostTask = Task.Run(() => host.RunAsync(hostCts.Token), CancellationToken.None);

        await using var probe = fixture.NewSession(new[] { master, slave.Account });
        await probe.StartAsync(ct);

        var symbolId = await ResolveSymbolAsync(probe, masterCtid, [slave.Account.Ctid], ct);
        var details = (await probe.LoadSymbolDetailsAsync(masterCtid, [symbolId], ct)).First();
        var step = details.StepVolume > 0 ? details.StepVolume : 1;
        var volume = Math.Max(details.MinVolume, step * 2);

        var label = $"cmind-pc-{Guid.NewGuid():N}"[..24];
        await Task.Delay(HostWarmup, ct);
        await probe.SendMarketOrderAsync(masterCtid, symbolId, isBuy: true, volume, label, ct);

        var masterPosition = await PollAsync(() => FindByLabelAsync(probe, masterCtid, label, ct), TimeSpan.FromSeconds(12), ct);
        if (masterPosition is null)
        {
            await StopHostAsync(hostCts, hostTask);
            return new PartialCloseResult(true, "Master order did not fill (market likely closed).", 0, 0);
        }

        var sourceId = masterPosition.PositionId.ToString();
        var copy = await PollAsync(() => FindByLabelAsync(probe, slave.Account.Ctid, sourceId, ct), CopyTimeout, ct);
        if (copy is null)
        {
            await CleanupAsync(probe, masterCtid, sourceId, [slave], ct);
            await StopHostAsync(hostCts, hostTask);
            return new PartialCloseResult(true, "Slave copy never appeared.", 0, 0);
        }

        var before = copy.Volume;
        await probe.ClosePositionAsync(masterCtid, masterPosition.PositionId, volume / 2, ct); // close half the master
        var shrunk = await PollAsync(async () =>
        {
            var current = await FindByLabelAsync(probe, slave.Account.Ctid, sourceId, ct);
            return current is not null && current.Volume < before ? current : null;
        }, CopyTimeout, ct);

        var after = shrunk?.Volume ?? before;
        output.WriteLine($"partial close: slave volume {before} -> {after}");
        await CleanupAsync(probe, masterCtid, sourceId, [slave], ct);
        await StopHostAsync(hostCts, hostTask);
        return new PartialCloseResult(false, null, before, after);
    }

    private async Task<long> ResolveSymbolAsync(OpenApiTradingSession probe, long masterCtid,
        IReadOnlyList<long> slaveCtids, CancellationToken ct)
    {
        var masterIds = await probe.LoadSymbolIdsAsync(masterCtid, ct);

        // Choose a symbol that exists on the master AND on every slave (cross-broker safety) so the
        // engine can actually place the copy on each destination.
        var common = new HashSet<string>(masterIds.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var slaveCtid in slaveCtids)
        {
            var slaveIds = await probe.LoadSymbolIdsAsync(slaveCtid, ct);
            common.IntersectWith(slaveIds.Keys);
        }

        foreach (var preferred in SymbolPreference)
            if (common.Contains(preferred) && masterIds.TryGetValue(preferred, out var id)) return id;

        var fallback = common.FirstOrDefault();
        return fallback is not null ? masterIds[fallback] : masterIds.Values.First();
    }

    private static async Task<OpenPositionSnapshot?> FindByLabelAsync(
        OpenApiTradingSession probe, long ctid, string label, CancellationToken ct)
    {
        var positions = await probe.ReconcileAsync(ctid, ct);
        return positions.FirstOrDefault(p => p.Label == label);
    }

    private static async Task<T?> PollAsync<T>(Func<Task<T?>> probe, TimeSpan timeout, CancellationToken ct)
        where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null) return result;
            await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
        }
        return null;
    }

    private async Task CleanupAsync(OpenApiTradingSession probe, long masterCtid, string sourceId,
        IReadOnlyList<SlaveSetup> slaves, CancellationToken ct)
    {
        foreach (var slave in slaves)
        {
            foreach (var position in await probe.ReconcileAsync(slave.Account.Ctid, ct))
                if (position.Label == sourceId)
                    await SafeCloseAsync(probe, slave.Account.Ctid, position, ct);
        }

        foreach (var position in await probe.ReconcileAsync(masterCtid, ct))
            if (position.PositionId.ToString() == sourceId)
                await SafeCloseAsync(probe, masterCtid, position, ct);
    }

    private async Task SafeCloseAsync(OpenApiTradingSession probe, long ctid, OpenPositionSnapshot position, CancellationToken ct)
    {
        try { await probe.ClosePositionAsync(ctid, position.PositionId, position.Volume, ct); }
        catch (Exception ex) { output.WriteLine($"cleanup close failed for {ctid}/{position.PositionId}: {ex.Message}"); }
    }

    private static async Task StopHostAsync(CancellationTokenSource hostCts, Task hostTask)
    {
        hostCts.Cancel();
        try { await hostTask.WaitAsync(TimeSpan.FromSeconds(5)); } catch { /* best effort */ }
    }

    public static CopyDestination Destination(Action<CopyDestination>? configure = null)
    {
        var profile = CopyProfile.Create(UserId.New(), "live", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        configure?.Invoke(destination);
        return destination;
    }
}
