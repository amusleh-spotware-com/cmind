using Microsoft.Extensions.Logging;

namespace Core.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Stats poll cycle failed")]
    public static partial void StatsPollFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Stats collection failed for node {NodeName}")]
    public static partial void NodeStatsFailed(this ILogger logger, string nodeName, Exception ex);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Reconcile cycle failed")]
    public static partial void ReconcileFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Starting container on {Host}: {Command}")]
    public static partial void StartingContainer(this ILogger logger, string host, string command);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Owner credentials not configured; skipping owner seed")]
    public static partial void OwnerCredentialsMissing(this ILogger logger);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Owner account seeded: {Email}")]
    public static partial void OwnerSeeded(this ILogger logger, string email);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Local node seeded: {Name}")]
    public static partial void LocalNodeSeeded(this ILogger logger, string name);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Local docker command failed: {Command} -> {Error}")]
    public static partial void LocalDockerFailed(this ILogger logger, string command, string error);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Error, Message = "Backtest completion poll cycle failed")]
    public static partial void BacktestPollFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Warning, Message = "Backtest status check failed for instance {InstanceId}")]
    public static partial void BacktestStatusCheckFailed(this ILogger logger, Guid instanceId, Exception ex);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Error, Message = "Run completion poll cycle failed")]
    public static partial void RunPollFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Warning, Message = "Run status check failed for instance {InstanceId}")]
    public static partial void RunStatusCheckFailed(this ILogger logger, Guid instanceId, Exception ex);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Warning, Message = "Local docker build failed: {Error}")]
    public static partial void LocalBuildFailed(this ILogger logger, string error);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Warning, Message = "AI request failed with status {StatusCode}: {Body}")]
    public static partial void AiRequestFailed(this ILogger logger, int statusCode, string body);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Error, Message = "AI request errored")]
    public static partial void AiRequestError(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Error, Message = "AI risk-guard cycle failed")]
    public static partial void RiskGuardFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "AI risk-guard assessed {Count} running bots: {Summary}")]
    public static partial void RiskGuardAssessment(this ILogger logger, int count, string summary);

    [LoggerMessage(EventId = 1017, Level = LogLevel.Error, Message = "Portfolio agent cycle failed")]
    public static partial void AgentCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1018, Level = LogLevel.Warning, Message = "Portfolio agent mandate {MandateId} failed")]
    public static partial void AgentMandateFailed(this ILogger logger, Guid mandateId, Exception ex);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Portfolio agent proposal {ProposalId} created for mandate {MandateId} (autonomy {Autonomy})")]
    public static partial void AgentProposalCreated(this ILogger logger, Guid proposalId, Guid mandateId, string autonomy);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "Portfolio agent proposal {ProposalId} executed -> instance {InstanceId}")]
    public static partial void AgentProposalExecuted(this ILogger logger, Guid proposalId, Guid instanceId);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Warning, Message = "Portfolio agent proposal {ProposalId} execution failed: {Reason}")]
    public static partial void AgentProposalExecutionFailed(this ILogger logger, Guid proposalId, string reason);
}
