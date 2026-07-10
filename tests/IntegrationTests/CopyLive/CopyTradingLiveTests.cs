using CTraderOpenApi.Client;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.CopyLive;

// End-to-end copy trading against real cTrader DEMO accounts. Fully automated + reproducible: the
// fixture refreshes the cached tokens, these tests open a real master position and assert the engine
// mirrors it onto the slave(s), then clean everything up. Only demo accounts are ever traded.
[Collection(LiveCopyCollection.Name)]
public sealed class CopyTradingLiveTests(LiveCopyFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Token_refreshes_and_lists_demo_accounts_live()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var account = fixture.DemoAccounts[0];

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var client = new OpenApiClient(fixture.ConnectionFactory);
        var grant = await client.LoadGrantAsync(fixture.ClientId, fixture.ClientSecret, account.AccessToken, cts.Token);

        output.WriteLine($"cid={account.Cid} ctidUser={grant.CtidUserId} accounts={grant.Accounts.Count}");
        grant.Accounts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task One_to_one_lot_multiplier_copies_master_position()
        => await RunAsync(SameCid(1), masterIsBuy: true, reverse: false, r =>
        {
            r.Slaves.Should().ContainSingle();
            r.Slaves[0].Copied.Should().BeTrue("the master position must be mirrored onto the slave");
            r.Slaves[0].IsBuy.Should().BeTrue("a non-reversed copy keeps the master's direction");
        });

    [Fact]
    public async Task One_to_many_copies_to_all_slaves()
        => await RunAsync(SameCid(2), masterIsBuy: true, reverse: false, r =>
        {
            r.Slaves.Should().HaveCount(2);
            r.Slaves.Should().OnlyContain(s => s.Copied, "every slave in a 1:many profile must be mirrored");
        });

    [Fact]
    public async Task Reverse_copies_the_opposite_side()
        => await RunAsync(SameCid(1), masterIsBuy: true, reverse: true, r =>
        {
            r.Slaves[0].Copied.Should().BeTrue();
            r.Slaves[0].IsBuy.Should().BeFalse("reverse copy mirrors a master buy as a slave sell");
        });

    [Fact]
    public async Task Copies_across_different_cids()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var master = fixture.DemoAccounts[0];
        var slave = fixture.DemoAccounts.FirstOrDefault(a => a.Cid != master.Cid);
        if (slave is null) { output.WriteLine("only one cID has demo accounts; cross-cID skipped"); return; }
        output.WriteLine($"cross-cID: master cid={master.Cid} -> slave cid={slave.Cid}");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunAsync(master, masterIsBuy: true,
            [new LiveCopyScenario.SlaveSetup(slave, LiveCopyScenario.Destination())], cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        result.Slaves[0].Copied.Should().BeTrue("a master under one cID must copy to a slave under another cID");
    }

    [Fact]
    public async Task Partial_close_shrinks_the_slave_copy_proportionally()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var accounts = SameCid(1);
        var master = accounts[0];
        var slave = new LiveCopyScenario.SlaveSetup(accounts[1], LiveCopyScenario.Destination());

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunPartialCloseAsync(master, slave, cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        result.SlaveVolumeAfter.Should().BeLessThan(result.SlaveVolumeBefore,
            "a master partial close must shrink the mirrored copy");
    }

    [Fact]
    public async Task Pending_limit_order_is_mirrored_and_cancel_propagates()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var accounts = SameCid(1);
        var slave = new LiveCopyScenario.SlaveSetup(accounts[1],
            LiveCopyScenario.Destination(d => d.SetPendingOrderCopying(true)));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunPendingAsync(accounts[0], slave, cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        result.SlavePendingAppeared.Should().BeTrue("a master limit order must be mirrored onto the slave");
        result.SlavePendingCancelled.Should().BeTrue("cancelling the master pending must cancel the slave pending");
    }

    [Fact]
    public async Task Trailing_stop_is_mirrored_onto_the_slave_copy()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var accounts = SameCid(1);
        var slave = new LiveCopyScenario.SlaveSetup(accounts[1],
            LiveCopyScenario.Destination(d => { d.SetCopyProtection(true, false); d.SetTrailingStopCopying(true); }));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunTrailingAsync(accounts[0], slave, cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        result.SlaveTrailing.Should().BeTrue("a master trailing stop must be mirrored onto the slave copy");
    }

    private async Task RunAsync(IReadOnlyList<LiveCopyFixture.LiveAccount> accounts, bool masterIsBuy,
        bool reverse, Action<LiveCopyScenario.ScenarioResult> assert)
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }

        var master = accounts[0];
        var slaves = accounts.Skip(1)
            .Select(a => new LiveCopyScenario.SlaveSetup(a,
                LiveCopyScenario.Destination(d => { if (reverse) d.SetReverse(true); })))
            .ToList();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output).RunAsync(master, masterIsBuy, slaves, cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }
        assert(result);
    }

    // Master + N slaves all under the same cID (so symbols/specs match exactly).
    private IReadOnlyList<LiveCopyFixture.LiveAccount> SameCid(int slaveCount)
    {
        var byCid = fixture.DemoAccounts.GroupBy(a => a.Cid)
            .FirstOrDefault(g => g.Count() > slaveCount);
        byCid.Should().NotBeNull($"need a cID with at least {slaveCount + 1} demo accounts");
        return byCid!.Take(slaveCount + 1).ToList();
    }
}
