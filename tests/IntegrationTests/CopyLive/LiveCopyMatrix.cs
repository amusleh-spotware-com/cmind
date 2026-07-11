using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static IntegrationTests.CopyLive.LiveCopyScenario;

namespace IntegrationTests.CopyLive;

// Data-driven live option matrix: one real master open per row, each with a differently-configured
// destination, asserting the golden outcome (copied / not copied, and direction) against real cTrader demo
// accounts. Skips cleanly without credentials and reports Inconclusive on a closed market. Every row cleans
// up after itself (LiveCopyScenario). This is the live counterpart to the deterministic DST suite.
[Collection(LiveCopyCollection.Name)]
public sealed class LiveCopyMatrix(LiveCopyFixture fixture, ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases() =>
    [
        ["one_to_one"],
        ["half_multiplier"],
        ["reverse"],
        ["manage_only"],
        ["trading_hours_closed"],
        ["source_label_block"],
        ["lot_sanity_block"],
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Copy_option_matrix(string caseName)
    {
        if (!fixture.Available) { output.WriteLine(fixture.SkipReason); return; }

        var spec = Resolve(caseName);
        var accounts = SameCid(1);
        var slave = new SlaveSetup(accounts[1], Destination(spec.Configure));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await new LiveCopyScenario(fixture, output)
            .RunAsync(accounts[0], spec.MasterIsBuy, [slave], cts.Token);

        if (result.Inconclusive) { output.WriteLine($"INCONCLUSIVE ({caseName}): {result.Reason}"); return; }

        var s = result.Slaves.Single();
        s.Copied.Should().Be(spec.ExpectCopied, $"[{caseName}] copied outcome");
        if (spec.ExpectCopied && spec.ExpectBuy is { } buy)
            s.IsBuy.Should().Be(buy, $"[{caseName}] copy direction");
    }

    private sealed record CaseSpec(Action<CopyDestination> Configure, bool MasterIsBuy, bool ExpectCopied, bool? ExpectBuy);

    private static CaseSpec Resolve(string caseName) => caseName switch
    {
        "one_to_one" => new CaseSpec(_ => { }, MasterIsBuy: true, ExpectCopied: true, ExpectBuy: true),
        "half_multiplier" => new CaseSpec(
            d => d.ConfigureRisk(new RiskSettings(MoneyManagementMode.LotMultiplier, 0.5)),
            true, true, true),
        "reverse" => new CaseSpec(d => d.SetReverse(true), true, true, ExpectBuy: false),
        "manage_only" => new CaseSpec(d => d.SetManageOnly(true), true, ExpectCopied: false, null),
        "trading_hours_closed" => new CaseSpec(d => d.ConfigureTradingHours(ClosedWindowNow()), true, false, null),
        "source_label_block" => new CaseSpec(d => d.SetSourceLabelFilter("no-such-label"), true, false, null),
        "lot_sanity_block" => new CaseSpec(d => d.ConfigureLotSanity(new LotSanityCeiling(0.001, 0)), true, false, null),
        _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "unknown matrix case")
    };

    // A one-minute trading window that does NOT contain the current UTC minute, so opens are skipped now.
    private static TradingWindow ClosedWindowNow()
    {
        var nowMinute = (int)DateTime.UtcNow.TimeOfDay.TotalMinutes;
        var start = (nowMinute + 30) % 1400; // <= 1399, and never equal to nowMinute
        return new TradingWindow(start, start + 1);
    }

    private IReadOnlyList<LiveCopyFixture.LiveAccount> SameCid(int slaveCount)
    {
        var byCid = fixture.DemoAccounts.GroupBy(a => a.Cid).FirstOrDefault(g => g.Count() > slaveCount);
        byCid.Should().NotBeNull($"need a cID with at least {slaveCount + 1} demo accounts");
        return byCid!.Take(slaveCount + 1).ToList();
    }
}
