using System.Net.Http.Json;
using Core.Ai;
using Core.Constants;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Azure OpenAI adapter — same Chat Completions body/response as <see cref="OpenAiCompatibleProvider"/>,
/// but authenticated with the <c>api-key</c> header and targeting the deployment path
/// <c>openai/deployments/{model}/chat/completions?api-version=...</c> (the model is the Azure deployment name).
/// </summary>
public sealed class AzureOpenAiProvider(HttpClient http, ILogger<AzureOpenAiProvider> logger) : IAiProvider
{
    public AiProviderKind Kind => AiProviderKind.AzureOpenAi;

    public async Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        var body = OpenAiCompatibleProvider.BuildBody(request);
        var path = $"{AzureOpenAiConstants.ChatCompletionsPath(request.Model)}" +
                   $"?{AzureOpenAiConstants.ApiVersionQuery}={AzureOpenAiConstants.ApiVersion}";

        using var msg = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(request.BaseUrl), path));
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) msg.Headers.Add(AzureOpenAiConstants.ApiKeyHeader, request.ApiKey);
        msg.Content = JsonContent.Create(body);

        return await AiWireHelpers.SendAsync(http, msg, OpenAiCompatibleProvider.ExtractText, logger, ct);
    }
}
