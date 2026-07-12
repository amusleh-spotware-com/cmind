using Core.Agent;
using Core.Calendar;
using Core.PropFirm;

namespace Core.Domain;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; private set; }

    public void StampOccurredAt(DateTimeOffset occurredAt)
    {
        if (OccurredAt == default) OccurredAt = occurredAt;
    }
}

public sealed record AlertRaised(AlertRuleId RuleId, UserId UserId, string Severity) : DomainEventBase;

public sealed record UserRegistered(UserId UserId, string NormalizedEmail) : DomainEventBase;

public sealed record UserEmailConfirmed(UserId UserId) : DomainEventBase;

public sealed record UserApproved(UserId UserId) : DomainEventBase;

public sealed record AgentProposalCreated(AgentProposalId ProposalId, AgentMandateId MandateId, UserId UserId)
    : DomainEventBase;

public sealed record AgentProposalDecided(
    AgentProposalId ProposalId, AgentMandateId MandateId, AgentProposalStatus Status) : DomainEventBase;

public sealed record InstanceStarted(InstanceId InstanceId, UserId UserId, string ContainerId) : DomainEventBase;

public sealed record InstanceStopped(InstanceId InstanceId, UserId UserId) : DomainEventBase;

public sealed record InstanceFailed(InstanceId InstanceId, UserId UserId, string Reason) : DomainEventBase;

public sealed record BacktestCompleted(InstanceId InstanceId, UserId UserId) : DomainEventBase;

public sealed record McpApiKeyRevoked(McpApiKeyId KeyId, UserId UserId) : DomainEventBase;

public sealed record OpenApiAccountAuthorized(
    OpenApiAuthorizationId AuthorizationId, UserId UserId, long CtidUserId) : DomainEventBase;

public sealed record AccessTokenRefreshed(
    OpenApiAuthorizationId AuthorizationId, UserId UserId, long CtidUserId) : DomainEventBase;

public sealed record AccessTokenRefreshFailed(
    OpenApiAuthorizationId AuthorizationId, UserId UserId, string Reason) : DomainEventBase;

public sealed record AccessTokenRefreshCritical(
    OpenApiAuthorizationId AuthorizationId, UserId UserId, long CtidUserId,
    DateTimeOffset AccessTokenExpiresAt, int ConsecutiveFailures) : DomainEventBase;

public sealed record CopyProfileStarted(CopyProfileId ProfileId, UserId UserId) : DomainEventBase;

public sealed record CopyProfilePaused(CopyProfileId ProfileId, UserId UserId) : DomainEventBase;

public sealed record CopyProfileStopped(CopyProfileId ProfileId, UserId UserId) : DomainEventBase;

public sealed record CopyProfileErrored(CopyProfileId ProfileId, UserId UserId, string Reason) : DomainEventBase;

public sealed record NodeRegistered(NodeId NodeId, string Name) : DomainEventBase;

public sealed record NodeWentOffline(NodeId NodeId, string Name) : DomainEventBase;

public sealed record NodeCameOnline(NodeId NodeId, string Name) : DomainEventBase;

public sealed record PropFirmChallengeStarted(PropFirmChallengeId ChallengeId, UserId UserId) : DomainEventBase;

public sealed record PropFirmPhasePassed(PropFirmChallengeId ChallengeId, UserId UserId, ChallengePhase NewPhase)
    : DomainEventBase;

public sealed record PropFirmChallengePassed(PropFirmChallengeId ChallengeId, UserId UserId) : DomainEventBase;

public sealed record PropFirmChallengeBreached(PropFirmChallengeId ChallengeId, UserId UserId, BreachReason Reason)
    : DomainEventBase;

public sealed record PropFirmChallengeStopped(PropFirmChallengeId ChallengeId, UserId UserId) : DomainEventBase;

public sealed record PropFirmDrawdownWarning(PropFirmChallengeId ChallengeId, UserId UserId, double PercentUsed)
    : DomainEventBase;

public sealed record EconomicEventReleased(
    CalendarEventId EventId, EconomicSeriesId SeriesId, decimal Actual, DateTimeOffset EffectiveAt)
    : DomainEventBase;

public sealed record EconomicEventRevised(
    CalendarEventId EventId, EconomicSeriesId SeriesId, decimal Actual, DateTimeOffset KnownAt) : DomainEventBase;

public sealed record EconomicEventRescheduled(
    CalendarEventId EventId, EconomicSeriesId SeriesId, DateTimeOffset PreviousInstant, DateTimeOffset NewInstant)
    : DomainEventBase;
