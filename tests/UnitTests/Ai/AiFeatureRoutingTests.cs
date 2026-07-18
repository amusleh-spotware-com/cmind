using Core;
using Core.Ai;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Ai;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiFeatureRoutingTests
{
    private sealed class CapturingClient : IAiClient
    {
        public AiTextRequest? Last { get; private set; }
        public bool Enabled => true;
        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(AiResult.Ok("ok"));
        }
    }

    [Fact]
    public async Task Feature_service_stamps_the_feature_on_each_request()
    {
        var client = new CapturingClient();
        var svc = new AiFeatureService(client);

        await svc.GenerateCBotAsync("CSharp", "desc", default);
        client.Last!.Feature.Should().Be(AiFeature.GenerateCBot);

        await svc.ReviewCBotAsync("CSharp", "src", default);
        client.Last!.Feature.Should().Be(AiFeature.ReviewCBot);

        await svc.DebateStrategyAsync("n", "CSharp", "src", 100, default);
        client.Last!.Feature.Should().Be(AiFeature.DebateStrategy);

        await svc.RecommendCopyProfileAsync("balanced", "src", default);
        client.Last!.Feature.Should().Be(AiFeature.RecommendCopyProfile);
    }

    [Fact]
    public void Binding_create_sets_scope_feature_and_credential()
    {
        var owner = UserId.New();
        var credential = AiProviderCredentialId.New();
        var now = DateTimeOffset.Parse("2026-07-18T00:00:00Z");

        var binding = AiFeatureBinding.Create(owner, AiFeature.GenerateCBot, credential, now);

        binding.OwnerUserId.Should().Be(owner);
        binding.IsDeploymentScoped.Should().BeFalse();
        binding.Feature.Should().Be(AiFeature.GenerateCBot);
        binding.CredentialId.Should().Be(credential);
    }

    [Fact]
    public void Deployment_binding_has_null_owner()
    {
        var binding = AiFeatureBinding.Create(
            null, AiFeature.ReviewCBot, AiProviderCredentialId.New(), DateTimeOffset.UnixEpoch);

        binding.IsDeploymentScoped.Should().BeTrue();
        binding.OwnerUserId.Should().BeNull();
    }

    [Fact]
    public void Retarget_points_the_binding_at_a_new_credential()
    {
        var binding = AiFeatureBinding.Create(
            UserId.New(), AiFeature.FixCBot, AiProviderCredentialId.New(), DateTimeOffset.UnixEpoch);
        var next = AiProviderCredentialId.New();

        binding.Retarget(next, DateTimeOffset.Parse("2026-07-18T01:00:00Z"));

        binding.CredentialId.Should().Be(next);
    }
}
