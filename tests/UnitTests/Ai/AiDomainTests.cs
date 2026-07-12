using Core;
using Core.Ai;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 12, 12, 0, 0, TimeSpan.Zero);

    // ---------------- AiModelId ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AiModelId_rejects_blank(string value)
    {
        var act = () => new AiModelId(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiModelRequired);
    }

    [Fact]
    public void AiModelId_trims() => new AiModelId("  gpt-4o ").Value.Should().Be("gpt-4o");

    // ---------------- AiEndpoint ----------------

    [Theory]
    [InlineData("http://localhost:11434/v1/")]
    [InlineData("http://127.0.0.1:1234/v1/")]
    [InlineData("http://192.168.1.5:8000/v1/")]
    [InlineData("http://ollama/v1/")]
    [InlineData("https://api.openai.com/v1/")]
    public void AiEndpoint_accepts_https_and_loopback_or_private_http(string value)
    {
        var act = () => new AiEndpoint(value);
        act.Should().NotThrow();
    }

    [Fact]
    public void AiEndpoint_rejects_plaintext_http_to_public_host()
    {
        var act = () => new AiEndpoint("http://api.openai.com/v1/");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiEndpointInsecure);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://host/x")]
    public void AiEndpoint_rejects_non_http(string value)
    {
        var act = () => new AiEndpoint(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiEndpointInvalid);
    }

    [Fact]
    public void AiEndpoint_appends_trailing_slash()
        => new AiEndpoint("https://api.openai.com/v1").Value.Should().Be("https://api.openai.com/v1/");

    // ---------------- Capabilities defaults ----------------

    [Fact]
    public void OpenAiCompatible_defaults_to_text_only_for_local_safety()
    {
        var caps = AiProviderCapabilities.DefaultFor(AiProviderKind.OpenAiCompatible);
        caps.SupportsWebSearch.Should().BeFalse();
        caps.SupportsVision.Should().BeFalse();
        caps.SupportsSystemRole.Should().BeTrue();
    }

    [Fact]
    public void Anthropic_defaults_full_capability()
    {
        var caps = AiProviderCapabilities.DefaultFor(AiProviderKind.Anthropic);
        caps.SupportsWebSearch.Should().BeTrue();
        caps.SupportsVision.Should().BeTrue();
    }

    // ---------------- AiProviderCredential aggregate ----------------

    private static AiProviderCredential Create(AiProviderKind kind = AiProviderKind.OpenAiCompatible, byte[]? key = null) =>
        AiProviderCredential.Create(kind, new AiEndpoint("http://localhost:11434/v1/"), new AiModelId("llama3.1:8b"),
            key, AiProviderCapabilities.DefaultFor(kind), 4000, Now);

    [Fact]
    public void Create_local_without_key_is_valid()
    {
        var c = Create();
        c.HasKey.Should().BeFalse();
        c.IsActive.Should().BeFalse();
        c.Model.Should().Be("llama3.1:8b");
    }

    [Fact]
    public void Create_rejects_out_of_range_max_tokens()
    {
        var act = () => AiProviderCredential.Create(AiProviderKind.Anthropic, new AiEndpoint("https://api.anthropic.com/"),
            new AiModelId("claude-opus-4-8"), null, AiProviderCapabilities.DefaultFor(AiProviderKind.Anthropic), 0, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiMaxTokensOutOfRange);
    }

    [Fact]
    public void Activate_and_deactivate_flip_state_and_stamp_updated()
    {
        var c = Create();
        c.Activate(Now);
        c.IsActive.Should().BeTrue();
        c.UpdatedAt.Should().Be(Now);

        var later = Now.AddMinutes(5);
        c.Deactivate(later);
        c.IsActive.Should().BeFalse();
        c.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Rotate_sets_key_and_retarget_changes_endpoint_and_model()
    {
        var c = Create();
        c.Rotate([1, 2, 3], Now);
        c.HasKey.Should().BeTrue();

        c.Retarget(new AiEndpoint("https://api.openai.com/v1/"), new AiModelId("gpt-4o"), 8000, Now);
        c.BaseUrl.Should().Be("https://api.openai.com/v1/");
        c.Model.Should().Be("gpt-4o");
        c.MaxTokens.Should().Be(8000);
    }

    [Fact]
    public void OverrideCapabilities_replaces_flags()
    {
        var c = Create();
        c.OverrideCapabilities(new AiProviderCapabilities(true, true, false, true), Now);
        c.Capabilities.SupportsVision.Should().BeTrue();
        c.Capabilities.SupportsSystemRole.Should().BeFalse();
    }
}
