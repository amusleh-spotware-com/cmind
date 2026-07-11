using Core.Ai;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

public class AiRecommendDisabledTests
{
    [Fact]
    public async Task Recommend_returns_failure_when_ai_not_configured()
    {
        using var http = new HttpClient();
        var client = new AnthropicAiClient(http,
            new StaticOptionsMonitor<AppOptions>(new AppOptions()), new NoKeyStore(), NullLogger<AnthropicAiClient>.Instance);
        var service = new AiFeatureService(client);

        service.Enabled.Should().BeFalse();

        var result = await service.RecommendCopyProfileAsync("balanced", "master account", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    private sealed class NoKeyStore : IAiKeyStore
    {
        public bool HasKey => false;
        public bool HasStoredKey => false;
        public string? CurrentKey => null;
        public Task SetKeyAsync(string apiKey, CancellationToken ct) => Task.CompletedTask;
        public Task ClearKeyAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
