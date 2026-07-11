using System.Net;
using Core.Ai;
using Core.Constants;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegrationTests;

public sealed class AiHttpResilienceTests
{
    private sealed class SequenceHandler(int failTimes, string successBody) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref _calls);
            return Task.FromResult(n <= failTimes
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(successBody) });
        }
    }

    private sealed class StaticMonitor(AppOptions value) : IOptionsMonitor<AppOptions>
    {
        public AppOptions CurrentValue => value;
        public AppOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<AppOptions, string?> listener) => null;
    }

    private sealed class FakeKeyStore : IAiKeyStore
    {
        public bool HasKey => true;
        public bool HasStoredKey => true;
        public string? CurrentKey => "sk-test";
        public Task SetKeyAsync(string apiKey, CancellationToken ct) => Task.CompletedTask;
        public Task ClearKeyAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Ai_client_retries_transient_failures_then_succeeds()
    {
        var handler = new SequenceHandler(failTimes: 2, successBody: """{"content":[{"type":"text","text":"ok"}]}""");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptionsMonitor<AppOptions>>(
            new StaticMonitor(new AppOptions { Ai = new AiOptions { BaseUrl = AiConstants.DefaultBaseUrl } }));
        services.AddSingleton<IAiKeyStore>(new FakeKeyStore());
        services.AddAiHttpClient();
        services.AddHttpClient<IAiClient, AnthropicAiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IAiClient>();

        var result = await client.CompleteAsync(new AiTextRequest("system", "user"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("ok");
        handler.Calls.Should().Be(3); // initial attempt + 2 retries
    }
}
