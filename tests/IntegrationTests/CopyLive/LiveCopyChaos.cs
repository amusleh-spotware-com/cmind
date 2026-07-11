using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static IntegrationTests.CopyLive.LiveCopyScenario;

namespace IntegrationTests.CopyLive;

// Live chaos: the copy engine meets a hostile starting condition against real cTrader demo accounts — the
// master already holds a position before the host starts, so convergence can only come from the start-up
// resync. Also asserts the Sync-Open-on-Start toggle is honored live. Deterministic socket-flap / token /
// rejection chaos is covered exhaustively by the DST stress suite; this asserts the live resync path.
// Skips without credentials, Inconclusive on a closed market, self-cleaning.
[Collection(LiveCopyCollection.Name)]
public sealed class LiveCopyChaos(LiveCopyFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Start_with_a_preexisting_master_position_resyncs_onto_the_slave()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var accounts = SameCid(1);
        var slave = new SlaveSetup(accounts[1], Destination()); // Sync-Open-on-Start defaults to true

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunStartWithOpenAsync(accounts[0], slave, cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        result.SlaveCopied.Should().BeTrue("the start-up resync must open a copy for the master's pre-existing position");
    }

    [Fact]
    public async Task Start_with_open_does_not_copy_when_sync_open_on_start_is_off()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var accounts = SameCid(1);
        var slave = new SlaveSetup(accounts[1],
            Destination(d => d.SetSyncPolicy(syncOpenOnStart: false, syncClosedOnStart: true)));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunStartWithOpenAsync(accounts[0], slave, cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        result.SlaveCopied.Should().BeFalse("with Sync-Open-on-Start off, a pre-existing master position is not copied at start");
    }

    private IReadOnlyList<LiveCopyFixture.LiveAccount> SameCid(int slaveCount)
    {
        var byCid = fixture.DemoAccounts.GroupBy(a => a.Cid).FirstOrDefault(g => g.Count() > slaveCount);
        byCid.Should().NotBeNull($"need a cID with at least {slaveCount + 1} demo accounts");
        return byCid!.Take(slaveCount + 1).ToList();
    }
}
