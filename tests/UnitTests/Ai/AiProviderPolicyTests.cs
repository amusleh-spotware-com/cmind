using Core.Ai;
using Core.Constants;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiProviderPolicyTests
{
    private static readonly AiEndpoint Cloud = new("https://api.openai.com/v1/");
    private static readonly AiEndpoint Local = new("http://localhost:11434/v1/");

    [Fact]
    public void Default_branding_allows_everything()
    {
        var b = new BrandingOptions();
        foreach (var kind in Enum.GetValues<AiProviderKind>())
            AiProviderPolicy.IsKindAllowed(kind, b).Should().BeTrue();
        var act = () => AiProviderPolicy.EnsureAllowed(AiProviderKind.BuiltInOnnx, new AiEndpoint("https://builtin.local/"), b);
        act.Should().NotThrow();
    }

    [Fact]
    public void Built_in_can_be_removed_by_white_label()
    {
        var b = new BrandingOptions { AllowBuiltInAi = false };
        AiProviderPolicy.IsKindAllowed(AiProviderKind.BuiltInOnnx, b).Should().BeFalse();
        var act = () => AiProviderPolicy.EnsureAllowed(AiProviderKind.BuiltInOnnx, new AiEndpoint("https://builtin.local/"), b);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiBuiltInNotAllowed);
    }

    [Fact]
    public void Allowed_kinds_restricts_the_set()
    {
        var b = new BrandingOptions { AllowedAiProviderKinds = ["Anthropic"] };
        AiProviderPolicy.IsKindAllowed(AiProviderKind.Anthropic, b).Should().BeTrue();
        AiProviderPolicy.IsKindAllowed(AiProviderKind.Gemini, b).Should().BeFalse();
        var act = () => AiProviderPolicy.EnsureAllowed(AiProviderKind.Gemini, Cloud, b);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiProviderKindNotAllowed);
    }

    [Fact]
    public void Local_providers_can_be_forbidden()
    {
        var b = new BrandingOptions { AllowLocalProviders = false };
        var localAct = () => AiProviderPolicy.EnsureAllowed(AiProviderKind.OpenAiCompatible, Local, b);
        localAct.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiLocalProviderNotAllowed);

        // A cloud OpenAI-compatible endpoint is still fine.
        var cloudAct = () => AiProviderPolicy.EnsureAllowed(AiProviderKind.OpenAiCompatible, Cloud, b);
        cloudAct.Should().NotThrow();
    }
}
