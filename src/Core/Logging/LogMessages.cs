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

    [LoggerMessage(EventId = 1022, Level = LogLevel.Warning, Message = "AI risk-guard auto-stopped instance {InstanceId}: {Reason}")]
    public static partial void RiskGuardStopped(this ILogger logger, Guid instanceId, string reason);

    [LoggerMessage(EventId = 1023, Level = LogLevel.Warning, Message = "AI risk-guard verdict ref {Ref} out of range (running count {Count})")]
    public static partial void RiskGuardStopSkipped(this ILogger logger, int @ref, int count);

    [LoggerMessage(EventId = 1024, Level = LogLevel.Warning, Message = "AI risk-guard instance {InstanceId} has no node; container not stopped")]
    public static partial void RiskGuardNodeMissing(this ILogger logger, Guid instanceId);

    [LoggerMessage(EventId = 1025, Level = LogLevel.Error, Message = "Alert evaluation cycle failed")]
    public static partial void AlertCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1026, Level = LogLevel.Warning, Message = "Alert rule {RuleId} evaluation failed")]
    public static partial void AlertRuleFailed(this ILogger logger, Guid ruleId, Exception ex);

    [LoggerMessage(EventId = 1027, Level = LogLevel.Information, Message = "Alert raised for rule {RuleId} (severity {Severity})")]
    public static partial void AlertRaised(this ILogger logger, Guid ruleId, string severity);

    [LoggerMessage(EventId = 1028, Level = LogLevel.Error, Message = "Prop-guard cycle failed")]
    public static partial void PropGuardCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1029, Level = LogLevel.Warning, Message = "Prop-guard rule {RuleId} enforcement failed")]
    public static partial void PropGuardRuleFailed(this ILogger logger, Guid ruleId, Exception ex);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Warning, Message = "Prop-guard flattened account {AccountId} ({Count} live instances stopped)")]
    public static partial void PropGuardFlattened(this ILogger logger, Guid accountId, int count);

    [LoggerMessage(EventId = 1031, Level = LogLevel.Information, Message = "Node self-registered: {Name} at {BaseUrl}")]
    public static partial void NodeSelfRegistered(this ILogger logger, string name, string baseUrl);

    [LoggerMessage(EventId = 1032, Level = LogLevel.Warning, Message = "Node {Name} marked unreachable (no heartbeat within TTL)")]
    public static partial void NodeMarkedUnreachable(this ILogger logger, string name);

    [LoggerMessage(EventId = 1034, Level = LogLevel.Error, Message = "Node heartbeat monitor cycle failed")]
    public static partial void HeartbeatMonitorFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1035, Level = LogLevel.Information, Message = "Agent registered with main at {MainUrl} as node {NodeId}")]
    public static partial void AgentRegistered(this ILogger logger, string mainUrl, Guid nodeId);

    [LoggerMessage(EventId = 1036, Level = LogLevel.Warning, Message = "Agent registration attempt failed: {Error}")]
    public static partial void AgentRegistrationFailed(this ILogger logger, string error);

    [LoggerMessage(EventId = 1037, Level = LogLevel.Warning, Message = "Node {Name} re-registered with mode {Requested} but is persisted as {Current}; mode change ignored (delete + re-register to change mode)")]
    public static partial void NodeModeChangeIgnored(this ILogger logger, string name, string requested, string current);

    [LoggerMessage(EventId = 1038, Level = LogLevel.Error, Message = "Open API token refresh cycle failed")]
    public static partial void OpenApiTokenRefreshCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1039, Level = LogLevel.Information, Message = "Open API access token refreshed for account {CtidTraderAccountId}")]
    public static partial void OpenApiTokenRefreshed(this ILogger logger, long ctidTraderAccountId);

    [LoggerMessage(EventId = 1040, Level = LogLevel.Warning, Message = "Open API token refresh failed for account {CtidTraderAccountId}: {Error}")]
    public static partial void OpenApiTokenRefreshFailedFor(this ILogger logger, long ctidTraderAccountId, string error);

    [LoggerMessage(EventId = 1041, Level = LogLevel.Warning, Message = "Open API token refresh skipped for account {CtidTraderAccountId}: application {ApplicationId} not found")]
    public static partial void OpenApiTokenRefreshApplicationMissing(this ILogger logger, long ctidTraderAccountId, Guid applicationId);

    [LoggerMessage(EventId = 1042, Level = LogLevel.Warning, Message = "Open API OAuth callback failed")]
    public static partial void OpenApiCallbackFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1043, Level = LogLevel.Error, Message = "Copy engine supervisor cycle failed")]
    public static partial void CopySupervisorFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1044, Level = LogLevel.Information, Message = "Copy profile {ProfileId} hosted")]
    public static partial void CopyProfileHosted(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 1045, Level = LogLevel.Warning, Message = "Copy profile {ProfileId} skipped: source or destinations are not Open API linked")]
    public static partial void CopyProfileNotLinkable(this ILogger logger, Guid profileId);

    // ---- Copy trading audit trail (every trading operation is logged for auditability) ----

    [LoggerMessage(EventId = 1046, Level = LogLevel.Information,
        Message = "Copy host started: profile {ProfileId} source {SourceCtid} -> {DestinationCount} destination(s)")]
    public static partial void CopyHostStarted(this ILogger logger, Guid profileId, long sourceCtid, int destinationCount);

    [LoggerMessage(EventId = 1047, Level = LogLevel.Information,
        Message = "Copy source open: profile {ProfileId} position {SourcePositionId} {Symbol} {Side} {Lots} lots")]
    public static partial void CopySourceOpen(this ILogger logger, Guid profileId, long sourcePositionId, string symbol, string side, double lots);

    [LoggerMessage(EventId = 1048, Level = LogLevel.Information,
        Message = "Copy order placed: profile {ProfileId} dest {DestinationCtid} {Symbol} {Side} volume {Volume} (source {SourcePositionId})")]
    public static partial void CopyOrderPlaced(this ILogger logger, Guid profileId, long destinationCtid, string symbol, string side, long volume, long sourcePositionId);

    [LoggerMessage(EventId = 1049, Level = LogLevel.Information,
        Message = "Copy skipped: profile {ProfileId} dest {DestinationCtid} source {SourcePositionId} reason {Reason}")]
    public static partial void CopySkipped(this ILogger logger, Guid profileId, long destinationCtid, long sourcePositionId, string reason);

    [LoggerMessage(EventId = 1050, Level = LogLevel.Information,
        Message = "Copy protection applied: profile {ProfileId} dest {DestinationCtid} source {SourcePositionId} SL {StopLoss} TP {TakeProfit}")]
    public static partial void CopyProtectionApplied(this ILogger logger, Guid profileId, long destinationCtid, long sourcePositionId, double stopLoss, double takeProfit);

    [LoggerMessage(EventId = 1051, Level = LogLevel.Warning,
        Message = "Copy open failed: profile {ProfileId} dest {DestinationCtid} source {SourcePositionId}")]
    public static partial void CopyOpenFailed(this ILogger logger, Guid profileId, long destinationCtid, long sourcePositionId, Exception ex);

    [LoggerMessage(EventId = 1052, Level = LogLevel.Information,
        Message = "Copy source close: profile {ProfileId} position {SourcePositionId}")]
    public static partial void CopySourceClose(this ILogger logger, Guid profileId, long sourcePositionId);

    [LoggerMessage(EventId = 1053, Level = LogLevel.Information,
        Message = "Copy position closed: profile {ProfileId} dest {DestinationCtid} position {PositionId} (source {SourcePositionId})")]
    public static partial void CopyPositionClosed(this ILogger logger, Guid profileId, long destinationCtid, long positionId, long sourcePositionId);

    [LoggerMessage(EventId = 1054, Level = LogLevel.Warning,
        Message = "Copy close failed: profile {ProfileId} dest {DestinationCtid} source {SourcePositionId}")]
    public static partial void CopyCloseFailed(this ILogger logger, Guid profileId, long destinationCtid, long sourcePositionId, Exception ex);

    [LoggerMessage(EventId = 1055, Level = LogLevel.Information,
        Message = "Copy resync: profile {ProfileId} {SourceOpen} source position(s) open, closed {OrphansClosed} orphaned copy(ies)")]
    public static partial void CopyResync(this ILogger logger, Guid profileId, int sourceOpen, int orphansClosed);

    [LoggerMessage(EventId = 1056, Level = LogLevel.Information,
        Message = "Copy partial close: profile {ProfileId} dest {DestinationCtid} position {PositionId} volume {Volume} (source {SourcePositionId})")]
    public static partial void CopyPartialClose(this ILogger logger, Guid profileId, long destinationCtid, long positionId, long volume, long sourcePositionId);

    [LoggerMessage(EventId = 1057, Level = LogLevel.Information,
        Message = "Copy scale-in: profile {ProfileId} dest {DestinationCtid} {Symbol} volume {Volume} (source {SourcePositionId})")]
    public static partial void CopyScaleIn(this ILogger logger, Guid profileId, long destinationCtid, string symbol, long volume, long sourcePositionId);

    [LoggerMessage(EventId = 1058, Level = LogLevel.Information,
        Message = "Copy pending order placed: profile {ProfileId} dest {DestinationCtid} {Symbol} {Kind} {Side} volume {Volume} price {Price} (source order {SourceOrderId})")]
    public static partial void CopyPendingOrderPlaced(this ILogger logger, Guid profileId, long destinationCtid, string symbol, string kind, string side, long volume, double price, long sourceOrderId);

    [LoggerMessage(EventId = 1059, Level = LogLevel.Information,
        Message = "Copy pending order cancelled: profile {ProfileId} dest {DestinationCtid} order {OrderId} (source order {SourceOrderId})")]
    public static partial void CopyPendingOrderCancelled(this ILogger logger, Guid profileId, long destinationCtid, long orderId, long sourceOrderId);

    [LoggerMessage(EventId = 1060, Level = LogLevel.Information,
        Message = "Copy trailing stop applied: profile {ProfileId} dest {DestinationCtid} source {SourcePositionId}")]
    public static partial void CopyTrailingApplied(this ILogger logger, Guid profileId, long destinationCtid, long sourcePositionId);

    [LoggerMessage(EventId = 1061, Level = LogLevel.Information,
        Message = "Copy stop-loss amended: profile {ProfileId} dest {DestinationCtid} source {SourcePositionId} SL {StopLoss}")]
    public static partial void CopyStopLossAmended(this ILogger logger, Guid profileId, long destinationCtid, long sourcePositionId, double stopLoss);

    [LoggerMessage(EventId = 1062, Level = LogLevel.Information,
        Message = "Copy host restarted for token rotation: profile {ProfileId}")]
    public static partial void CopyHostTokenRotated(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 1063, Level = LogLevel.Information,
        Message = "Copy pending order amended: profile {ProfileId} dest {DestinationCtid} order {OrderId} (source order {SourceOrderId})")]
    public static partial void CopyPendingOrderAmended(this ILogger logger, Guid profileId, long destinationCtid, long orderId, long sourceOrderId);

    [LoggerMessage(EventId = 1064, Level = LogLevel.Information,
        Message = "Copy market-range slippage mirrored: profile {ProfileId} dest {DestinationCtid} source {SourceId} slippagePoints {SlippagePoints}")]
    public static partial void CopyMarketRangeSlippage(this ILogger logger, Guid profileId, long destinationCtid, long sourceId, int slippagePoints);

    [LoggerMessage(EventId = 1065, Level = LogLevel.Information,
        Message = "Copy pending expiry mirrored: profile {ProfileId} dest {DestinationCtid} source order {SourceOrderId} expiry {ExpirationTimestamp}")]
    public static partial void CopyPendingExpiryMirrored(this ILogger logger, Guid profileId, long destinationCtid, long sourceOrderId, long expirationTimestamp);

    [LoggerMessage(EventId = 1066, Level = LogLevel.Information,
        Message = "Copy host token swapped in place: profile {ProfileId} account {Ctid}")]
    public static partial void CopyHostTokenSwapped(this ILogger logger, Guid profileId, long ctid);

    [LoggerMessage(EventId = 1078, Level = LogLevel.Warning,
        Message = "Copy access token invalidated: profile {ProfileId} account {Ctid} ({Code}) — awaiting refreshed token to auto-recover")]
    public static partial void CopyTokenInvalidated(this ILogger logger, Guid profileId, long ctid, string code);

    [LoggerMessage(EventId = 1079, Level = LogLevel.Warning,
        Message = "Copy host restarted by watchdog: profile {ProfileId} (previous host exited/faulted)")]
    public static partial void CopyHostRestarted(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 1080, Level = LogLevel.Warning,
        Message = "Copy destination tripped: profile {ProfileId} dest {DestinationCtid} after {Failures} consecutive failures — new opens paused for {CooldownSeconds}s")]
    public static partial void CopyDestinationTripped(this ILogger logger, Guid profileId, long destinationCtid, int failures, double cooldownSeconds);

    [LoggerMessage(EventId = 1081, Level = LogLevel.Warning,
        Message = "Copy account protection triggered: profile {ProfileId} dest {DestinationCtid} mode {Mode} at equity {Equity} (stop {StopEquity})")]
    public static partial void CopyAccountProtectionTriggered(this ILogger logger, Guid profileId, long destinationCtid, string mode, double equity, double stopEquity);

    [LoggerMessage(EventId = 1082, Level = LogLevel.Warning,
        Message = "Copy prop-rule breached: profile {ProfileId} dest {DestinationCtid} {Rule} at equity {Equity} — auto-flattened and locked out for the day")]
    public static partial void CopyPropRuleBreached(this ILogger logger, Guid profileId, long destinationCtid, string rule, double equity);

    // ---- Prop-firm challenge tracking ----

    [LoggerMessage(EventId = 1067, Level = LogLevel.Error, Message = "Prop-firm tracking supervisor cycle failed")]
    public static partial void PropFirmSupervisorFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1068, Level = LogLevel.Information, Message = "Prop-firm challenge {ChallengeId} tracker hosted")]
    public static partial void PropFirmChallengeHosted(this ILogger logger, Guid challengeId);

    [LoggerMessage(EventId = 1069, Level = LogLevel.Warning,
        Message = "Prop-firm challenge {ChallengeId} skipped: trading account is not Open API linked")]
    public static partial void PropFirmChallengeNotTrackable(this ILogger logger, Guid challengeId);

    [LoggerMessage(EventId = 1070, Level = LogLevel.Information,
        Message = "Prop-firm tracker started: challenge {ChallengeId} account {Ctid}")]
    public static partial void PropFirmTrackerStarted(this ILogger logger, Guid challengeId, long ctid);

    [LoggerMessage(EventId = 1071, Level = LogLevel.Information,
        Message = "Prop-firm equity recorded: challenge {ChallengeId} equity {Equity} status {Status}")]
    public static partial void PropFirmEquityRecorded(this ILogger logger, Guid challengeId, decimal equity, string status);

    [LoggerMessage(EventId = 1072, Level = LogLevel.Warning, Message = "Prop-firm tracker for challenge {ChallengeId} failed")]
    public static partial void PropFirmTrackerFailed(this ILogger logger, Guid challengeId, Exception ex);

    [LoggerMessage(EventId = 1073, Level = LogLevel.Information,
        Message = "Prop-firm tracker token swapped in place: challenge {ChallengeId} account {Ctid}")]
    public static partial void PropFirmTrackerTokenSwapped(this ILogger logger, Guid challengeId, long ctid);

    [LoggerMessage(EventId = 1074, Level = LogLevel.Information,
        Message = "Prop-firm challenge {ChallengeId} resolved: {Status} ({Breach})")]
    public static partial void PropFirmChallengeResolved(this ILogger logger, Guid challengeId, string status, string breach);

    [LoggerMessage(EventId = 1075, Level = LogLevel.Information,
        Message = "Prop-firm alert: challenge {ChallengeId} PASSED for user {UserId}")]
    public static partial void PropFirmAlertPassed(this ILogger logger, Guid challengeId, Guid userId);

    [LoggerMessage(EventId = 1076, Level = LogLevel.Warning,
        Message = "Prop-firm alert: challenge {ChallengeId} FAILED ({Reason}) for user {UserId}")]
    public static partial void PropFirmAlertBreached(this ILogger logger, Guid challengeId, string reason, Guid userId);

    [LoggerMessage(EventId = 1077, Level = LogLevel.Warning,
        Message = "Prop-firm alert: challenge {ChallengeId} drawdown at {PercentUsed}% for user {UserId}")]
    public static partial void PropFirmAlertDrawdownWarning(this ILogger logger, Guid challengeId, double percentUsed, Guid userId);
}
