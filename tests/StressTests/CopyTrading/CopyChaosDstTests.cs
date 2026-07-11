using FluentAssertions;
using Xunit;

namespace StressTests.CopyTrading;

// The deterministic-simulation core: one seed drives a randomized mix of every hostile event the copy
// engine can meet in production — bursts of opens, partial closes, scale-ins, closes, socket flaps,
// token rotations and order-rejection toggles — interleaved unpredictably. At the end all faults are
// healed and a final reconnect forces a resync; the world must converge and the host must never fault.
// A failure prints its seed, which reproduces the exact run.
public sealed class CopyChaosDstTests
{
    private const int Steps = 400;
    private static readonly TimeSpan Converge = TimeSpan.FromSeconds(20);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(101)]
    [InlineData(1234)]
    [InlineData(31337)]
    [InlineData(2026)]
    public async Task Randomized_chaos_workload_converges_and_never_faults(int seed)
    {
        var random = new Random(seed);
        await using var world = new CopyDstWorld(destinationCount: 12,
            configure: d => d.SetPartialCloseMirroring(mirrorPartialClose: true, mirrorScaleIn: true));

        var open = new List<long>();
        var faulted = new HashSet<long>();

        for (var step = 0; step < Steps; step++)
        {
            switch (random.Next(100))
            {
                case < 34:
                    open.Add(world.OpenSource(isBuy: random.Next(2) == 0, volume: (random.Next(1, 4)) * 200));
                    break;
                case < 50 when open.Count > 0:
                    world.PartialCloseSource(TakeRandom(random, open, remove: false), newVolume: 200);
                    break;
                case < 60 when open.Count > 0:
                    world.ScaleInSource(TakeRandom(random, open, remove: false), addedVolume: 200);
                    break;
                case < 80 when open.Count > 0:
                    await world.CloseSource(TakeRandom(random, open, remove: true));
                    break;
                case < 88:
                    await world.FlapConnectionAsync();
                    break;
                case < 94:
                    world.RotateTokens($"seed{seed}-step{step}");
                    break;
                default:
                    ToggleFault(world, random, faulted);
                    break;
            }

            if (step % 40 == 39) await Task.Delay(5);
        }

        // heal everything, then reconcile until the drained event stream and the master book agree
        foreach (var ctid in faulted.ToArray())
            world.HealOrders(ctid);
        faulted.Clear();

        world.HostFaulted.Should().BeFalse($"seed {seed} must not fault the host: {world.HostFault}");
        (await world.ReconcileUntilConvergedAsync(Converge))
            .Should().BeTrue($"seed {seed} must converge every destination to the master book");
    }

    private static long TakeRandom(Random random, List<long> ids, bool remove)
    {
        var index = random.Next(ids.Count);
        var id = ids[index];
        if (remove) ids.RemoveAt(index);
        return id;
    }

    private static void ToggleFault(CopyDstWorld world, Random random, HashSet<long> faulted)
    {
        var ctid = world.Destinations[random.Next(world.Destinations.Count)];
        if (faulted.Add(ctid)) world.FailOrders(ctid);
        else { faulted.Remove(ctid); world.HealOrders(ctid); }
    }
}
