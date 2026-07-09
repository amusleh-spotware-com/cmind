using Microsoft.Extensions.Logging;

namespace CTraderOpenApi.Logging;

public static partial class OpenApiLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Open API connected to {Host}:{Port}")]
    public static partial void Connected(this ILogger logger, string host, int port);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Open API connection dropped: {Reason}. Reconnecting.")]
    public static partial void ConnectionDropped(this ILogger logger, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Open API fatal error {Code}: {Description}")]
    public static partial void FatalError(this ILogger logger, string code, string? description);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Open API maintenance until {EndsAt}; delaying reconnect")]
    public static partial void MaintenanceWait(this ILogger logger, DateTimeOffset? endsAt);
}
