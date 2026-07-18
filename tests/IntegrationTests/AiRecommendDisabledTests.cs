using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai;
using Xunit;

namespace IntegrationTests;

public class AiRecommendDisabledTests
{
    [Fact]
    public async Task Recommend_returns_failure_when_no_provider_configured()
    {
        var client = new RoutingAiClient(new NoActiveStore(), []);
        var service = new AiFeatureService(client);

        service.Enabled.Should().BeFalse();

        var result = await service.RecommendCopyProfileAsync("balanced", "master account", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    private sealed class NoActiveStore : IAiProviderStore
    {
        public bool HasActive => false;
        public ActiveAiProvider? Active => null;
        public Task<IReadOnlyList<AiProviderView>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiProviderView>>([]);
        public Task<Guid> UpsertAsync(UpsertAiProviderCommand command, CancellationToken ct) => Task.FromResult(Guid.NewGuid());
        public Task ActivateAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AiProviderView>> ListForUserAsync(Core.UserId user, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiProviderView>>([]);
        public Task<Guid> UpsertForUserAsync(Core.UserId user, UpsertAiProviderCommand command, CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());
        public Task ActivateForUserAsync(Core.UserId user, Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveForUserAsync(Core.UserId user, Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task SeedFromConfigAsync(CancellationToken ct) => Task.CompletedTask;
        public ActiveAiProvider? ResolveFor(AiFeature? feature, Core.AiProviderCredentialId? credentialId) => null;
        public Task<IReadOnlyList<AiFeatureBindingView>> ListBindingsAsync(Core.UserId? owner, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiFeatureBindingView>>([]);
        public Task SetBindingAsync(Core.UserId? owner, AiFeature feature, Core.AiProviderCredentialId credentialId, CancellationToken ct) =>
            Task.CompletedTask;
        public Task ClearBindingAsync(Core.UserId? owner, AiFeature feature, CancellationToken ct) => Task.CompletedTask;
    }
}
