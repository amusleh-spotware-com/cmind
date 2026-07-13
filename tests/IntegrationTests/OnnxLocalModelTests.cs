using Core.Ai;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests;

// Opt-in real built-in-LLM lane (non-UI). Runs a genuine completion through the ONNX GenAI provider when
// a model directory is supplied via AI_ONNX_MODEL (e.g. a Phi-3-mini ONNX folder). Skipped cleanly
// otherwise — the model weights are large, so this is a nightly / on-demand lane, not default CI.
public sealed class OnnxLocalModelTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Built_in_onnx_model_completes_a_prompt()
    {
        var modelPath = Environment.GetEnvironmentVariable("AI_ONNX_MODEL");
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            output.WriteLine("skipped: set AI_ONNX_MODEL to an ONNX GenAI model directory to run this lane");
            return;
        }

        var options = new StaticOptionsMonitor<AppOptions>(
            new AppOptions { Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { ModelPath = modelPath, MaxTokens = 128 } } });
        using var provider = new OnnxGenAiProvider(options, NullLogger<OnnxGenAiProvider>.Instance);

        provider.IsModelPresent().Should().BeTrue($"AI_ONNX_MODEL should point to a model dir with genai_config.json: {modelPath}");

        var request = new AiProviderRequest(
            AiProviderKind.BuiltInOnnx, "https://builtin.local/", "built-in-onnx", null, 128,
            AiProviderCapabilities.DefaultFor(AiProviderKind.BuiltInOnnx),
            System: "You are terse.", User: "Reply with the single word: ok.", EnableWebSearch: false, Image: null);

        var result = await provider.CompleteAsync(request, CancellationToken.None);

        output.WriteLine($"onnx reply: {result.Text} (err: {result.Error})");
        result.Success.Should().BeTrue();
        result.Text.Should().NotBeNullOrWhiteSpace();
    }

    // Regression: a prompt whose token count exceeds the output-token budget (MaxTokens) must still
    // complete. ONNX GenAI "max_length" is the TOTAL (prompt + generated) budget; the provider used to
    // set it to just MaxTokens, so a long prompt (e.g. the currency-strength forward prompt at ~1.7k
    // tokens vs a 1024 budget) threw "input sequence_length (N) is >= max_length (M)".
    [Fact]
    public async Task Built_in_onnx_model_completes_a_prompt_longer_than_the_output_budget()
    {
        var modelPath = Environment.GetEnvironmentVariable("AI_ONNX_MODEL");
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            output.WriteLine("skipped: set AI_ONNX_MODEL to an ONNX GenAI model directory to run this lane");
            return;
        }

        var options = new StaticOptionsMonitor<AppOptions>(
            new AppOptions { Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { ModelPath = modelPath, MaxTokens = 64 } } });
        using var provider = new OnnxGenAiProvider(options, NullLogger<OnnxGenAiProvider>.Instance);
        provider.IsModelPresent().Should().BeTrue();

        // A user prompt far longer (in tokens) than the 64-token output budget.
        var longUser = string.Join(' ', Enumerable.Repeat("Summarise the following market note in one word.", 60));
        var request = new AiProviderRequest(
            AiProviderKind.BuiltInOnnx, "https://builtin.local/", "built-in-onnx", null, 64,
            AiProviderCapabilities.DefaultFor(AiProviderKind.BuiltInOnnx),
            System: "You are terse.", User: longUser, EnableWebSearch: false, Image: null);

        var result = await provider.CompleteAsync(request, CancellationToken.None);

        output.WriteLine($"onnx long-prompt reply: {result.Text} (err: {result.Error})");
        result.Success.Should().BeTrue("a prompt longer than the output budget must not throw on max_length");
        result.Text.Should().NotBeNullOrWhiteSpace();
    }
}
