using Core;
using Core.Agent;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Agent;

// Invariants + transitions for the AgentProposal decision lifecycle: create, approve/reject (once),
// execute, and fail. (WS-1 Core backfill.)
public class AgentProposalTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 17, 0, 0, TimeSpan.Zero);

    private static AgentProposal NewProposal()
        => AgentProposal.Create(AgentMandateId.New(), UserId.New(), AgentConstants.ProposalKindBacktest,
            "reasoning", "  ", "prop-1");

    [Fact]
    public void Create_defaults_blank_payload_and_guards_name()
    {
        var proposal = NewProposal();
        proposal.PayloadJson.Should().Be("{}");
        proposal.ProposedName.Should().Be("prop-1");
        proposal.Status.Should().Be(AgentProposalStatus.Pending);

        var blank = () => AgentProposal.Create(AgentMandateId.New(), UserId.New(), "k", "r", "{}", " ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Approve_moves_to_approved_and_stamps_the_decider()
    {
        var proposal = NewProposal();
        var decider = UserId.New();

        proposal.Approve(decider, Now);

        proposal.Status.Should().Be(AgentProposalStatus.Approved);
        proposal.DecidedByUserId.Should().Be(decider);
        proposal.DecidedAt.Should().Be(Now);
    }

    [Fact]
    public void Reject_moves_to_rejected()
    {
        var proposal = NewProposal();
        proposal.Reject(UserId.New(), Now);
        proposal.Status.Should().Be(AgentProposalStatus.Rejected);
    }

    [Fact]
    public void Deciding_a_non_pending_proposal_throws()
    {
        var proposal = NewProposal();
        proposal.Approve(UserId.New(), Now);

        var again = () => proposal.Reject(UserId.New(), Now);
        again.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.ProposalNotPending);
    }

    [Fact]
    public void Mark_executed_records_created_ids()
    {
        var proposal = NewProposal();
        var paramSetId = ParamSetId.New();
        var instanceId = InstanceId.New();

        proposal.MarkExecuted(paramSetId, instanceId);

        proposal.Status.Should().Be(AgentProposalStatus.Executed);
        proposal.CreatedParamSetId.Should().Be(paramSetId);
        proposal.CreatedInstanceId.Should().Be(instanceId);
    }

    [Fact]
    public void Mark_failed_records_reason_and_decider()
    {
        var proposal = NewProposal();
        var decider = UserId.New();

        proposal.MarkFailed(decider, "backtest crashed", Now);

        proposal.Status.Should().Be(AgentProposalStatus.Failed);
        proposal.FailureReason.Should().Be("backtest crashed");
        proposal.DecidedByUserId.Should().Be(decider);
    }
}
