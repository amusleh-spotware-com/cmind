using Core;
using Core.Ai;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiProviderCredentialScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 12, 12, 00, 00, TimeSpan.Zero);

    private static AiProviderCredential Create(UserId? owner) =>
        AiProviderCredential.Create(
            AiProviderKind.Anthropic,
            new AiEndpoint("https://api.anthropic.com/"),
            new AiModelId("claude-opus-4-8"),
            null,
            AiProviderCapabilities.DefaultFor(AiProviderKind.Anthropic),
            8000,
            Now,
            owner);

    [Fact]
    public void Create_without_owner_is_deployment_scoped()
    {
        var credential = Create(owner: null);

        credential.OwnerUserId.Should().BeNull();
        credential.IsDeploymentScoped.Should().BeTrue();
    }

    [Fact]
    public void Create_with_owner_is_user_scoped()
    {
        var uid = UserId.New();

        var credential = Create(owner: uid);

        credential.OwnerUserId.Should().Be(uid);
        credential.IsDeploymentScoped.Should().BeFalse();
    }
}
