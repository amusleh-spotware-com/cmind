using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

// Drives OpenAiCompatibleProvider end-to-end against a real (in-process) OpenAI-compatible HTTP server.
// This is the deterministic "local LLM" lane every CI run exercises.
public sealed class LocalLlmProviderTests
{
    [Fact]
    public async Task OpenAiCompatible_provider_talks_to_a_local_openai_endpoint_end_to_end()
    {
        using var server = new FakeLocalLlmServer(reply: "hello from local");
        var provider = new OpenAiCompatibleProvider(new HttpClient(), NullLogger<OpenAiCompatibleProvider>.Instance);

        var request = new AiProviderRequest(
            AiProviderKind.OpenAiCompatible, server.BaseUrl, "llama3.1:8b", ApiKey: null, MaxTokens: 128,
            AiProviderCapabilities.DefaultFor(AiProviderKind.OpenAiCompatible),
            System: "You are helpful.", User: "hi", EnableWebSearch: false, Image: null);

        var result = await provider.CompleteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("hello from local");
    }
}
