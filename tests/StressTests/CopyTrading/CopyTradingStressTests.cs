using FluentAssertions;
using Xunit;

namespace StressTests.CopyTrading;

// Copy-trading stress scenarios driven through the DST world. Each asserts that the copy engine
// survives a hostile workload and that every healthy destination converges to the master's live state.
public sealed class CopyTradingStressTests
{
    private static readonly TimeSpan Converge = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Mass_fan_out_mirrors_every_open_to_every_destination_then_drains_on_close()
    {
        await using var world = new CopyDstWorld(destinationCount: 80);
        var ids = new List<long>();
        for (var i = 0; i < 150; i++)
            ids.Add(world.OpenSource(isBuy: i % 2 == 0, volume: 100));

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue("all 150 opens must fan out to all 80 destinations");
        world.HostFaulted.Should().BeFalse();

        foreach (var id in ids)
            await world.CloseSource(id);

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue("every destination must drain to empty after all closes");
    }

    [Fact]
    public async Task High_frequency_open_close_leaves_no_orphans()
    {
        await using var world = new CopyDstWorld(destinationCount: 20);
        for (var i = 0; i < 300; i++)
        {
            var id = world.OpenSource(isBuy: i % 3 == 0, volume: 100);
            if (i % 2 == 0) await world.CloseSource(id);
        }

        // close whatever is still open
        foreach (var id in world.OpenSourceIds.ToArray())
            await world.CloseSource(id);

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue();
        world.HostFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task Partial_close_and_scale_in_storm_converges()
    {
        await using var world = new CopyDstWorld(destinationCount: 15,
            configure: d => d.SetPartialCloseMirroring(mirrorPartialClose: true, mirrorScaleIn: true));
        var ids = new List<long>();
        for (var i = 0; i < 40; i++)
            ids.Add(world.OpenSource(isBuy: true, volume: 400));

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue();

        foreach (var id in ids)
        {
            world.ScaleInSource(id, addedVolume: 200);
            world.PartialCloseSource(id, newVolume: 200);
        }

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue("label set is stable across partial closes and scale-ins");
        world.HostFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task Connection_flap_storm_reconverges_after_every_resync()
    {
        await using var world = new CopyDstWorld(destinationCount: 25);
        var ids = new List<long>();
        for (var i = 0; i < 60; i++)
        {
            ids.Add(world.OpenSource(isBuy: i % 2 == 0, volume: 100));
            if (i % 10 == 9) await world.FlapConnectionAsync();
        }

        // desync while "offline": close half the book, then flap to force a reconcile-driven resync
        foreach (var id in ids.Take(30))
            await world.CloseSource(id);
        await world.FlapConnectionAsync();

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue("resync must close orphans and open missing to match the master");
        world.HostFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task Order_rejection_cascade_spares_healthy_destinations_then_self_heals()
    {
        await using var world = new CopyDstWorld(destinationCount: 20);
        var faulted = world.Destinations.Take(5).ToArray();
        foreach (var ctid in faulted)
            world.FailOrders(ctid);

        for (var i = 0; i < 80; i++)
            world.OpenSource(isBuy: i % 2 == 0, volume: 100);

        // healthy destinations converge despite the rejection cascade on the faulted subset
        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue();
        world.HostFaulted.Should().BeFalse("per-destination order rejections must not fault the host");

        foreach (var ctid in faulted)
        {
            var labels = await world.DestinationLabelsAsync(ctid);
            labels.Should().BeEmpty("a destination rejecting every order must not have mirrored anything");
        }

        // heal + reconnect: the previously-failing destinations must catch up via resync
        foreach (var ctid in faulted)
            world.HealOrders(ctid);
        await world.FlapConnectionAsync();

        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue("healed destinations reconcile to the full master book");
    }

    [Fact]
    public async Task Token_rotation_storm_keeps_copying_and_converges()
    {
        await using var world = new CopyDstWorld(destinationCount: 15);
        for (var i = 0; i < 60; i++)
        {
            world.OpenSource(isBuy: i % 2 == 0, volume: 100);
            if (i % 15 == 14) world.RotateTokens($"rotated-{i}");
        }

        world.RotateTokens("final-token");
        (await world.WaitForConvergenceAsync(Converge)).Should().BeTrue("in-place token swaps must not drop or duplicate copies");
        world.HostFaulted.Should().BeFalse();

        world.Session.SwapCount.Should().BeGreaterThan(0);
        world.Session.CurrentToken(world.Destinations[0]).Should().Be("final-token");
    }
}
