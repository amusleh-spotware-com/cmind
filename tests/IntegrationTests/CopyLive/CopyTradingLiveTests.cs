using CTraderOpenApi.Client;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.CopyLive;

[Collection(LiveCopyCollection.Name)]
public sealed class CopyTradingLiveTests(LiveCopyFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Token_refreshes_and_lists_demo_accounts_live()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var client = new OpenApiClient(fixture.ConnectionFactory);
        var grant = await client.LoadGrantAsync(fixture.ClientId, fixture.ClientSecret, fixture.AccessToken, cts.Token);

        output.WriteLine($"ctidUserId={grant.CtidUserId}, accounts={grant.Accounts.Count}");
        grant.Accounts.Should().HaveCountGreaterThanOrEqualTo(2,
            "copy trading needs at least a master and a slave account under the cID");
    }

    [Fact]
    public async Task One_to_one_lot_multiplier_copies_master_position()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var (master, slaves) = Accounts(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var scenario = new LiveCopyScenario(fixture, output);
        var result = await scenario.RunAsync(master, masterIsBuy: true,
            [new LiveCopyScenario.SlaveSetup(slaves[0], LiveCopyScenario.Destination())], cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }

        result.Slaves.Should().ContainSingle();
        result.Slaves[0].Copied.Should().BeTrue("the master position must be mirrored onto the slave");
        result.Slaves[0].IsBuy.Should().BeTrue("a non-reversed copy keeps the master's direction");
    }

    [Fact]
    public async Task One_to_many_copies_to_all_slaves()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var (master, slaves) = Accounts(2);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var scenario = new LiveCopyScenario(fixture, output);
        var result = await scenario.RunAsync(master, masterIsBuy: true,
            slaves.Select(s => new LiveCopyScenario.SlaveSetup(s, LiveCopyScenario.Destination())).ToList(),
            cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }

        result.Slaves.Should().HaveCount(2);
        result.Slaves.Should().OnlyContain(s => s.Copied, "every slave in a 1:many profile must be mirrored");
    }

    [Fact]
    public async Task Reverse_copies_the_opposite_side()
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }
        var (master, slaves) = Accounts(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var scenario = new LiveCopyScenario(fixture, output);
        var reverse = LiveCopyScenario.Destination(d => d.SetReverse(true));
        var result = await scenario.RunAsync(master, masterIsBuy: true,
            [new LiveCopyScenario.SlaveSetup(slaves[0], reverse)], cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE: {result.Reason}"); return; }

        result.Slaves[0].Copied.Should().BeTrue();
        result.Slaves[0].IsBuy.Should().BeFalse("reverse copy mirrors a master buy as a slave sell");
    }

    private (long Master, IReadOnlyList<long> Slaves) Accounts(int slaveCount)
    {
        var ctids = fixture.Accounts.Select(a => a.CtidTraderAccountId).ToList();
        ctids.Count.Should().BeGreaterThan(slaveCount, "need a master plus enough slaves");
        return (ctids[0], ctids.Skip(1).Take(slaveCount).ToList());
    }
}
