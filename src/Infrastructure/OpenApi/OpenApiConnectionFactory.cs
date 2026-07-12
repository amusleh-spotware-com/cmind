using Core.Options;
using CTraderOpenApi;
using CTraderOpenApi.Client;
using CTraderOpenApi.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.OpenApi;

public sealed class OpenApiConnectionFactory(
    IOptionsMonitor<AppOptions> options,
    IOpenApiTransportFactory transportFactory,
    IOpenApiRateLimitProvider rateLimits,
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : IOpenApiConnectionFactory
{
    public OpenApiConnection Create(bool live, string clientId, string clientSecret)
    {
        var settings = options.CurrentValue.OpenApi;
        var host = live ? settings.LiveHost : settings.DemoHost;
        var connectionOptions = new OpenApiConnectionOptions
        {
            HeartbeatInterval = settings.HeartbeatInterval,
            RequestTimeout = settings.RequestTimeout,
            InboundWatchdogTimeout = settings.InboundWatchdogTimeout,
            BackoffInitial = settings.BackoffInitial,
            BackoffMax = settings.BackoffMax,
            RateLimits = rateLimits.GetEffectiveLimits()
        };

        return new OpenApiConnection(
            transportFactory,
            host,
            settings.Port,
            clientId,
            clientSecret,
            connectionOptions,
            loggerFactory.CreateLogger<OpenApiConnection>(),
            timeProvider);
    }
}
