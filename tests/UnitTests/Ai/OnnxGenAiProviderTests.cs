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
    private static OnnxGenAiProvider Create(string modelPath)
    {
        var options = Substitute.For<IOptionsMonitor<AppOptions>>();
        options.CurrentValue.Returns(new AppOptions { Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { ModelPath = modelPath } } });
        return new OnnxGenAiProvider(options, NullLogger<OnnxGenAiProvider>.Instance);
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
}
