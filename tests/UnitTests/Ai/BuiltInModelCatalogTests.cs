using Core.Ai;
using Core.Constants;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Xunit;

namespace UnitTests.Ai;

public sealed class BuiltInModelCatalogTests
{
    [Fact]
    public void Default_is_the_shipped_model_and_carries_the_generic_key()
    {
        BuiltInModelCatalog.Default.IsDefault.Should().BeTrue();
        BuiltInModelCatalog.Default.Key.Should().Be(AiConstants.BuiltInModel);
        BuiltInModelCatalog.All.Should().Contain(BuiltInModelCatalog.Default);
    }

    [Fact]
    public void Catalog_keys_are_unique_and_every_spec_has_a_config_and_weights()
    {
        BuiltInModelCatalog.All.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        BuiltInModelCatalog.All.Should().OnlyContain(s => s.Files.Contains("genai_config.json"));
        BuiltInModelCatalog.All.Should()
            .OnlyContain(s => s.Files.Count(f => f.EndsWith(".onnx", System.StringComparison.Ordinal)) == 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("does-not-exist")]
    public void ForKey_falls_back_to_default_for_blank_or_unknown(string? key)
        => BuiltInModelCatalog.ForKey(key).Should().Be(BuiltInModelCatalog.Default);

    [Fact]
    public void ForKey_returns_the_matching_spec_case_insensitively()
    {
        var second = BuiltInModelCatalog.All.First(s => !s.IsDefault);
        BuiltInModelCatalog.ForKey(second.Key.ToUpperInvariant()).Should().Be(second);
    }

    // The generic/default/blank id loads the ModelPath root; a submodel key whose sub-directory is an
    // installed model loads that sub-directory; an unknown/uninstalled key falls back to the root.
    [Fact]
    public void SelectModelDir_maps_the_credential_model_to_its_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "onnx-select-" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(root, "phi-3-mini-128k");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "genai_config.json"), "{}");

        try
        {
            OnnxGenAiProvider.SelectModelDir(root, null).Should().Be(root);
            OnnxGenAiProvider.SelectModelDir(root, "").Should().Be(root);
            OnnxGenAiProvider.SelectModelDir(root, AiConstants.BuiltInModel).Should().Be(root);
            OnnxGenAiProvider.SelectModelDir(root, "not-installed").Should().Be(root);
            OnnxGenAiProvider.SelectModelDir(root, "phi-3-mini-128k").Should().Be(sub);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }
}
