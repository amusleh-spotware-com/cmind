using Core.Ai;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests;

// Opt-in real-local-LLM lane. Spins a real Ollama container, pulls a tiny model, and runs one genuine
// completion through OpenAiCompatibleProvider — the same adapter every local runtime uses. Skipped by
// default (pull weight / CI time); enable with AI_LOCAL_LLM=1 (nightly job or a local run) per the
// "self-serviceable, never excuse-skip live" repo rule.
public sealed class OllamaLocalLlmTests(ITestOutputHelper output)
{
    private const string Model = "tinyllama";

    [Fact]
    public async Task Real_ollama_completes_through_the_openai_compatible_adapter()
    {
        if (Environment.GetEnvironmentVariable("AI_LOCAL_LLM") != "1")
        {
            output.WriteLine("skipped: set AI_LOCAL_LLM=1 to run the real Ollama lane");
            return;
        }

        IContainer container = new ContainerBuilder()
            .WithImage("ollama/ollama:latest")
            .WithPortBinding(11434, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(11434))
            .Build();

        await container.StartAsync();
        try
        {
            var pull = await container.ExecAsync(["ollama", "pull", Model]);
            pull.ExitCode.Should().Be(0, pull.Stderr);

            var baseUrl = $"http://{container.Hostname}:{container.GetMappedPublicPort(11434)}/v1/";
            var provider = new OpenAiCompatibleProvider(new HttpClient { Timeout = TimeSpan.FromMinutes(5) },
                NullLogger<OpenAiCompatibleProvider>.Instance);

            var request = new AiProviderRequest(
                AiProviderKind.OpenAiCompatible, baseUrl, Model, ApiKey: null, MaxTokens: 64,
                AiProviderCapabilities.DefaultFor(AiProviderKind.OpenAiCompatible),
                System: "You are terse.", User: "Reply with the single word: ok.", EnableWebSearch: false, Image: null);

            var result = await provider.CompleteAsync(request, CancellationToken.None);

            output.WriteLine($"ollama reply: {result.Text} (err: {result.Error})");
            result.Success.Should().BeTrue();
            result.Text.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            await container.DisposeAsync();
        }
    }
}
