using Core.Ai;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegrationTests;

// Drives AiModelCatalog end-to-end against a real (in-process) OpenAI-compatible HTTP server that serves
// GET /v1/models — the deterministic "local LLM" model-discovery lane (LM Studio / Ollama / vLLM wire).
public sealed class AiModelCatalogIntegrationTests
{
    private sealed class FixedOptionsMonitor(AppOptions value) : IOptionsMonitor<AppOptions>
    {
        public AppOptions CurrentValue { get; } = value;
        public AppOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AppOptions, string?> listener) => null;
    }

    [Fact]
    public async Task Lists_models_from_a_real_local_openai_endpoint()
    {
        using var server = new FakeLocalLlmServer(reply: "unused");
        var catalog = new AiModelCatalog(
            new HttpClient(), new FixedOptionsMonitor(new AppOptions()), NullLogger<AiModelCatalog>.Instance);

        var models = await catalog.ListModelsAsync(
            AiProviderKind.OpenAiCompatible, new AiEndpoint(server.BaseUrl), apiKey: null, CancellationToken.None);

        models.Select(m => m.Id).Should().Contain(FakeLocalLlmServer.AdvertisedModelId);
    }
}
