using Core.Ai;
using Core.Constants;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace UnitTests.Ai;

public sealed class OnnxGenAiProviderTests
{
    private static OnnxGenAiProvider Create(string modelPath, bool autoDownload = false, IBuiltInModelInstaller? installer = null)
    {
        var options = Substitute.For<IOptionsMonitor<AppOptions>>();
        options.CurrentValue.Returns(new AppOptions
        {
            Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { ModelPath = modelPath, AutoDownload = autoDownload } }
        });
        return new OnnxGenAiProvider(options, NullLogger<OnnxGenAiProvider>.Instance, installer);
    }

    private static AiProviderRequest Request() =>
        new(AiProviderKind.BuiltInOnnx, "https://builtin.local/", "built-in-onnx", null, 256,
            AiProviderCapabilities.DefaultFor(AiProviderKind.BuiltInOnnx), "sys", "user", false, null);

    [Fact]
    public void Kind_is_built_in_onnx() => Create("nope").Kind.Should().Be(AiProviderKind.BuiltInOnnx);

    [Fact]
    public void Model_absent_reports_not_present()
        => Create(Path.Combine(Path.GetTempPath(), "no-such-onnx-model-dir")).IsModelPresent().Should().BeFalse();

    [Fact]
    public async Task Degrades_to_typed_failure_when_model_absent()
    {
        using var provider = Create(Path.Combine(Path.GetTempPath(), "no-such-onnx-model-dir"));
        var result = await provider.CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Be(AiConstants.BuiltInUnavailableMessage);
    }

    [Fact]
    public async Task Auto_download_kicks_off_install_and_reports_downloading()
    {
        var installer = Substitute.For<IBuiltInModelInstaller>();
        installer.StateOf(BuiltInModelCatalog.Default.Key).Returns(BuiltInModelInstallState.Downloading);
        using var provider = Create(Path.Combine(Path.GetTempPath(), "no-such-onnx-model-dir"),
            autoDownload: true, installer: installer);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        installer.Received(1).EnsureInstalling(BuiltInModelCatalog.Default.Key);
        result.Success.Should().BeFalse();
        result.Error.Should().Be(AiConstants.BuiltInDownloadingMessage);
    }

    [Fact]
    public async Task Failed_download_falls_back_to_install_hint()
    {
        var installer = Substitute.For<IBuiltInModelInstaller>();
        installer.StateOf(BuiltInModelCatalog.Default.Key).Returns(BuiltInModelInstallState.Failed);
        using var provider = Create(Path.Combine(Path.GetTempPath(), "no-such-onnx-model-dir"),
            autoDownload: true, installer: installer);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);
        result.Error.Should().Be(AiConstants.BuiltInUnavailableMessage);
    }

    // max_length must never exceed the model's context window (ORT GenAI throws otherwise). A short prompt
    // gets prompt + output budget; a prompt near the window is capped at the window.
    [Theory]
    [InlineData(100, 1024, 4096, 1124)]     // fits with headroom
    [InlineData(4000, 1024, 4096, 4096)]    // capped at the window
    [InlineData(18000, 1024, 131072, 19024)] // large cBot-gen prompt fits a 128k model
    public void ComputeMaxLength_caps_at_context_window(int promptTokens, int maxTokens, int contextLength, int expected)
        => OnnxGenAiProvider.ComputeMaxLength(promptTokens, maxTokens, contextLength).Should().Be(expected);

    // A prompt that alone fills/exceeds the window can't generate — the caller degrades to a typed failure
    // instead of ORT GenAI throwing (the reported "max_length (19337) > context_length (4096)" crash).
    [Theory]
    [InlineData(4096, 10, 4096)]     // prompt exactly fills the window
    [InlineData(18313, 1024, 4096)]  // the reported crash: 18k prompt on a 4k model
    public void ComputeMaxLength_returns_null_when_prompt_does_not_fit(int promptTokens, int maxTokens, int contextLength)
        => OnnxGenAiProvider.ComputeMaxLength(promptTokens, maxTokens, contextLength).Should().BeNull();
}
