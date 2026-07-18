using Core.Ai;
using Core.Constants;
using Infrastructure.Ai.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace Infrastructure.Ai;

/// <summary>
/// Registers one shared, resilience-wrapped <see cref="System.Net.Http.HttpClient"/> (generous
/// per-attempt/total timeouts, bounded retry on transient 5xx/network failures) and every provider
/// adapter over it. All adapters reuse the identical <see cref="AiHttp"/> pipeline, so the resilience,
/// retry, and typed-failure guarantee are the same for cloud and local (OpenAI-compatible) endpoints —
/// a dead Ollama retries then degrades exactly like a throttled Anthropic. Base URL/headers are set
/// per request, so one client serves them all.
/// </summary>
public static class AiHttpClientRegistration
{
    public static IServiceCollection AddAiHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient(AiConstants.HttpClientName)
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

        services.AddTransient<IAiProvider>(sp => new AnthropicAiProvider(
            Client(sp), sp.GetRequiredService<ILogger<AnthropicAiProvider>>()));
        services.AddTransient<IAiProvider>(sp => new OpenAiCompatibleProvider(
            Client(sp), sp.GetRequiredService<ILogger<OpenAiCompatibleProvider>>()));
        services.AddTransient<IAiProvider>(sp => new AzureOpenAiProvider(
            Client(sp), sp.GetRequiredService<ILogger<AzureOpenAiProvider>>()));
        services.AddTransient<IAiProvider>(sp => new GeminiAiProvider(
            Client(sp), sp.GetRequiredService<ILogger<GeminiAiProvider>>()));
        // Model discovery (browse an endpoint's models) reuses the same resilient "ai" client.
        services.AddTransient<IAiModelCatalog>(sp => new AiModelCatalog(
            Client(sp),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Core.Options.AppOptions>>(),
            sp.GetRequiredService<ILogger<AiModelCatalog>>()));

        // Built-in demo — no HttpClient, no key, no endpoint.
        services.AddTransient<IAiProvider, DemoAiProvider>();
        // Built-in real local LLM (ONNX) — singleton so the model loads once and is reused.
        services.AddSingleton<IAiProvider, OnnxGenAiProvider>();

        // Built-in model auto-download: a dedicated long-timeout client (model weights are large) + the
        // single-flight installer, so the built-in AI works out of the box with no manual provisioning.
        services.AddHttpClient(BuiltInModelInstaller.HttpClientName)
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<IBuiltInModelInstaller, BuiltInModelInstaller>();

        return services;
    }

    private static System.Net.Http.HttpClient Client(IServiceProvider sp) =>
        sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient(AiConstants.HttpClientName);
}
