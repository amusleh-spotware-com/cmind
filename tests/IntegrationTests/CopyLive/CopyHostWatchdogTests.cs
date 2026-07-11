using Core;
using FluentAssertions;
using Nodes.CopyTrading;
using Xunit;

namespace IntegrationTests.CopyLive;

// M2 (Phase 0.5): the supervisor watchdog restarts a wedged/dead host instead of leaving it stuck (the
// "just restart it" pain the prior copier only cured by hand). Pure decision, no DB — the ReconcileAsync
// wiring drops a dead host so the hosting loop recreates it, isolated per profile.
public sealed class CopyHostWatchdogTests
{
    [Fact]
    public void A_completed_host_task_for_an_owned_profile_is_dead()
    {
        var id = CopyProfileId.New();
        IReadOnlySet<CopyProfileId> mine = new HashSet<CopyProfileId> { id };

        CopyEngineSupervisor.IsHostDead(Task.CompletedTask, id, mine).Should().BeTrue();
    }

    [Fact]
    public void A_faulted_host_task_for_an_owned_profile_is_dead()
    {
        var id = CopyProfileId.New();
        IReadOnlySet<CopyProfileId> mine = new HashSet<CopyProfileId> { id };
        var faulted = Task.FromException(new InvalidOperationException("host wedged"));

        CopyEngineSupervisor.IsHostDead(faulted, id, mine).Should().BeTrue();
    }

    [Fact]
    public void A_running_host_task_is_not_dead()
    {
        var id = CopyProfileId.New();
        IReadOnlySet<CopyProfileId> mine = new HashSet<CopyProfileId> { id };
        var running = new TaskCompletionSource().Task;

        CopyEngineSupervisor.IsHostDead(running, id, mine).Should().BeFalse();
    }

    [Fact]
    public void A_host_for_a_profile_no_longer_owned_is_not_restarted()
    {
        var id = CopyProfileId.New();
        IReadOnlySet<CopyProfileId> mine = new HashSet<CopyProfileId>(); // reassigned to another node

        CopyEngineSupervisor.IsHostDead(Task.CompletedTask, id, mine).Should().BeFalse();
    }
}
