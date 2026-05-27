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
}
