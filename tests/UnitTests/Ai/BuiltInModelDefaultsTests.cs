using Core.Constants;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

// Locks the shipped built-in ONNX model download config so a bad edit to the constants (wrong folder,
// missing weights, mismatched quant folder vs file name) fails fast instead of only surfacing on the
// opt-in real-download E2E. The shipped default is Phi-3.5-mini-instruct (cpu-int4-awq).
public sealed class BuiltInModelDefaultsTests
{
    [Fact]
    public void Download_base_url_is_the_phi_3_5_mini_instruct_onnx_cpu_folder()
    {
        AiConstants.BuiltInModelDownloadBaseUrl.Should().StartWith("https://");
        AiConstants.BuiltInModelDownloadBaseUrl.Should().EndWith("/");
        AiConstants.BuiltInModelDownloadBaseUrl.Should()
            .Contain("microsoft/Phi-3.5-mini-instruct-onnx")
            .And.Contain("cpu-int4-awq-block-128-acc-level-4");
    }

    [Fact]
    public void Download_files_include_the_config_tokenizer_and_exactly_one_onnx_plus_weights()
    {
        var files = AiConstants.BuiltInModelDownloadFiles;
        files.Should().NotBeEmpty();
        files.Should().OnlyHaveUniqueItems();
        files.Should().NotContain(f => string.IsNullOrWhiteSpace(f));

        // GenAI needs the config + a tokenizer to load the model.
        files.Should().Contain("genai_config.json");
        files.Should().Contain("tokenizer.json");

        // Exactly one ONNX graph and its external weights blob.
        files.Count(f => f.EndsWith(".onnx", System.StringComparison.Ordinal)).Should().Be(1);
        files.Count(f => f.EndsWith(".onnx.data", System.StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void Onnx_weight_file_name_matches_the_cpu_int4_awq_quant_folder()
    {
        var onnx = AiConstants.BuiltInModelDownloadFiles
            .Single(f => f.EndsWith(".onnx", System.StringComparison.Ordinal));
        // The weight file name encodes the same quant as the folder — a mismatch means a broken download.
        onnx.Should().Be("phi-3.5-mini-instruct-cpu-int4-awq-block-128-acc-level-4.onnx");
    }
}
