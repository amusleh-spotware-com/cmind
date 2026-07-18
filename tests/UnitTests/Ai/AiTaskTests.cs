using Core;
using Core.Ai;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiTaskTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static AiTask NewTask() =>
        AiTask.Create(UserId.New(), AiFeature.GenerateCBot, AiProviderCredentialId.New(), """{"description":"x"}""", Now);

    [Fact]
    public void Create_starts_queued_with_no_lease()
    {
        var task = NewTask();
        task.Status.Should().Be(AiTaskStatus.Queued);
        task.IsActive.Should().BeTrue();
        task.StartedAt.Should().BeNull();
        task.ClaimedBy.Should().BeNull();
        task.IsClaimable(Now).Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_a_blank_payload()
    {
        var act = () => AiTask.Create(UserId.New(), AiFeature.GenerateCBot, AiProviderCredentialId.New(), "  ", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiTaskPromptRequired);
    }

    [Fact]
    public void Claim_moves_queued_to_running_under_a_lease()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);

        task.Status.Should().Be(AiTaskStatus.Running);
        task.ClaimedBy.Should().Be("node-a");
        task.LeaseExpiresAt.Should().Be(Now + Lease);
        task.StartedAt.Should().Be(Now);
    }

    [Fact]
    public void A_live_leased_running_task_is_not_claimable_by_another_node()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);

        task.IsClaimable(Now.AddMinutes(1)).Should().BeFalse();
        var act = () => task.Claim("node-b", Now.AddMinutes(1), Lease);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiTaskNotClaimable);
    }

    [Fact]
    public void An_orphaned_running_task_is_reclaimable_after_the_lease_expires()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);

        var afterExpiry = Now + Lease + TimeSpan.FromSeconds(1);
        task.IsClaimable(afterExpiry).Should().BeTrue();
        task.Claim("node-b", afterExpiry, Lease);

        task.ClaimedBy.Should().Be("node-b");
        task.StartedAt.Should().Be(Now); // preserved across reclaim
    }

    [Fact]
    public void RenewLease_extends_only_while_running()
    {
        var task = NewTask();
        var act = () => task.RenewLease(Now, Lease);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiTaskNotClaimable);

        task.Claim("node-a", Now, Lease);
        task.RenewLease(Now.AddMinutes(2), Lease);
        task.LeaseExpiresAt.Should().Be(Now.AddMinutes(2) + Lease);
    }

    [Fact]
    public void Succeed_sets_result_clears_lease_and_becomes_terminal()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);
        task.Succeed("generated code", """{"cBotId":"abc"}""", Now.AddMinutes(1));

        task.Status.Should().Be(AiTaskStatus.Succeeded);
        task.IsTerminal.Should().BeTrue();
        task.ResultText.Should().Be("generated code");
        task.ResultRefsJson.Should().Be("""{"cBotId":"abc"}""");
        task.FinishedAt.Should().Be(Now.AddMinutes(1));
        task.ClaimedBy.Should().BeNull();
        task.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Fail_records_error_and_becomes_terminal()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);
        task.Fail("build failed", Now.AddMinutes(1));

        task.Status.Should().Be(AiTaskStatus.Failed);
        task.Error.Should().Be("build failed");
        task.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_is_allowed_from_queued_and_running()
    {
        var queued = NewTask();
        queued.Cancel(Now);
        queued.Status.Should().Be(AiTaskStatus.Cancelled);

        var running = NewTask();
        running.Claim("node-a", Now, Lease);
        running.Cancel(Now.AddMinutes(1));
        running.Status.Should().Be(AiTaskStatus.Cancelled);
    }

    [Fact]
    public void A_terminal_task_rejects_further_transitions()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);
        task.Succeed("ok", null, Now.AddMinutes(1));

        var claim = () => task.Claim("node-b", Now.AddMinutes(2), Lease);
        var fail = () => task.Fail("x", Now.AddMinutes(2));
        var cancel = () => task.Cancel(Now.AddMinutes(2));

        claim.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiTaskAlreadyTerminal);
        fail.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiTaskAlreadyTerminal);
        cancel.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiTaskAlreadyTerminal);
    }

    [Fact]
    public void Log_appends_sequenced_lines_and_is_a_no_op_once_terminal()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);
        task.Log("attempt 1", Now);
        task.Log("attempt 2", Now.AddSeconds(1));

        task.Logs.Select(l => l.Message).Should().Equal("attempt 1", "attempt 2");
        task.Logs.Select(l => l.Sequence).Should().Equal(0, 1);

        task.Succeed("ok", null, Now.AddMinutes(1));
        task.Log("late line", Now.AddMinutes(2));
        task.Logs.Should().HaveCount(2); // no-op after terminal
    }

    [Fact]
    public void RecordAttempt_increments_the_counter()
    {
        var task = NewTask();
        task.Claim("node-a", Now, Lease);
        task.RecordAttempt(Now);
        task.RecordAttempt(Now.AddSeconds(1));
        task.Attempts.Should().Be(2);
    }
}
