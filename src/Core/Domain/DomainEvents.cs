using Core.Agent;

namespace Core.Domain;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record AlertRaised(AlertRuleId RuleId, UserId UserId, string Severity) : DomainEventBase;

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

public sealed record CopyProfileStarted(CopyProfileId ProfileId, UserId UserId) : DomainEventBase;

public sealed record CopyProfilePaused(CopyProfileId ProfileId, UserId UserId) : DomainEventBase;

public sealed record CopyProfileStopped(CopyProfileId ProfileId, UserId UserId) : DomainEventBase;

public sealed record CopyProfileErrored(CopyProfileId ProfileId, UserId UserId, string Reason) : DomainEventBase;

public sealed record NodeRegistered(NodeId NodeId, string Name) : DomainEventBase;

public sealed record NodeWentOffline(NodeId NodeId, string Name) : DomainEventBase;

public sealed record NodeCameOnline(NodeId NodeId, string Name) : DomainEventBase;
