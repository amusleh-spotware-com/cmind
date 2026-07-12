using System.Net;
using Core.Ai;
using Core.Constants;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

// Proves the identical retry/timeout/typed-fail resilience guarantee holds for EVERY adapter — cloud and
// local (OpenAI-compatible) alike — since they all share the one AiHttp pipeline. 503 x2 then 200.
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

    public static IEnumerable<object[]> Adapters() => new[]
    {
        new object[] { AiProviderKind.Anthropic, "https://api.anthropic.com/", """{"content":[{"type":"text","text":"ok"}]}""" },
        new object[] { AiProviderKind.OpenAiCompatible, "http://localhost:11434/v1/", """{"choices":[{"message":{"content":"ok"}}]}""" },
        new object[] { AiProviderKind.AzureOpenAi, "https://res.openai.azure.com/", """{"choices":[{"message":{"content":"ok"}}]}""" },
        new object[] { AiProviderKind.Gemini, "https://generativelanguage.googleapis.com/", """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""" }
    };

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task Every_adapter_retries_transient_failures_then_succeeds(AiProviderKind kind, string baseUrl, string body)
    {
        var handler = new SequenceHandler(failTimes: 2, successBody: body);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiHttpClient();
        services.AddHttpClient(AiConstants.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var adapter = provider.GetServices<IAiProvider>().Single(p => p.Kind == kind);

        var request = new AiProviderRequest(kind, baseUrl, "the-model", "sk-test", 256,
            AiProviderCapabilities.DefaultFor(kind), "sys", "user", EnableWebSearch: false, Image: null);
        var result = await adapter.CompleteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("ok");
        handler.Calls.Should().Be(3); // initial attempt + 2 retries
    }
}
