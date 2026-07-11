using Core.Ai;
using Core.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Infrastructure.Ai;

/// <summary>
/// Registers the Anthropic-backed <see cref="IAiClient"/> with a resilience pipeline: generous
/// per-attempt and total timeouts (completions can be long-running) and a bounded retry on transient
/// 5xx / network failures. The client itself always degrades to a typed <see cref="AiResult"/> failure,
/// so a provider outage never throws into a page, MCP tool, or hosted service.
/// </summary>
public static class AiHttpClientRegistration
{
    public static IServiceCollection AddAiHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient<IAiClient, AnthropicAiClient>()
            .AddResilienceHandler("ai", builder =>
            {
                builder.AddTimeout(TimeSpan.FromSeconds(AiHttp.TotalTimeoutSeconds));
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = AiHttp.RetryCount,
                    Delay = TimeSpan.FromSeconds(AiHttp.RetryBaseDelaySeconds),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                });
                builder.AddTimeout(TimeSpan.FromSeconds(AiHttp.AttemptTimeoutSeconds));
            });

        return services;
    }
}
