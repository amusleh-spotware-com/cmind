using Core.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Nodes;

/// <summary>
/// Registers the node-agent HTTP clients with per-purpose resilience. Reads are idempotent and
/// retried on transient failures; writes are non-idempotent and only timed out (a retried start could
/// double-launch a container); the log stream is long-lived and gets no pipeline at all.
/// </summary>
internal static class NodeHttpClients
{
    public static IServiceCollection AddNodeAgentHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient(HttpContainerDispatcher.ReadClientName)
            .AddResilienceHandler("node-agent-read", builder =>
            {
                builder.AddTimeout(TimeSpan.FromSeconds(NodeAgentHttp.ReadTotalTimeoutSeconds));
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = NodeAgentHttp.ReadRetryCount,
                    Delay = TimeSpan.FromMilliseconds(NodeAgentHttp.ReadRetryBaseDelayMilliseconds),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                });
                builder.AddTimeout(TimeSpan.FromSeconds(NodeAgentHttp.ReadAttemptTimeoutSeconds));
            });

        services.AddHttpClient(HttpContainerDispatcher.WriteClientName)
            .AddResilienceHandler("node-agent-write", builder =>
                builder.AddTimeout(TimeSpan.FromSeconds(NodeAgentHttp.WriteTimeoutSeconds)));

        services.AddHttpClient(HttpContainerDispatcher.StreamClientName,
            client => client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }
}
