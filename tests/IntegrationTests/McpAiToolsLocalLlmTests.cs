extern alias mcp;
using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai;
using Infrastructure.Ai.Providers;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AiTools = mcp::Mcp.Tools.AiTools;

namespace IntegrationTests;

// MCP AI tools go through the same IAiFeatureService → IAiClient seam as the web endpoints, so a provider
// swap is transparent to MCP clients. This drives the MCP AiTools against the in-process fake local LLM
// (OpenAI wire) and asserts the tools return its deterministic reply — the MCP surface's AI E2E.
public sealed class McpAiToolsLocalLlmTests
{
    private const string Reply = "MCP-LOCAL-LLM-OK";

    private sealed class LocalStore(string baseUrl) : IAiProviderStore
    {
        public bool HasActive => true;
        public ActiveAiProvider? Active => new(AiProviderKind.OpenAiCompatible, baseUrl, "fake-model", null,
            AiProviderCapabilities.DefaultFor(AiProviderKind.OpenAiCompatible), 256);
        public Task<IReadOnlyList<AiProviderView>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiProviderView>>([]);
        public Task<Guid> UpsertAsync(UpsertAiProviderCommand command, CancellationToken ct) => Task.FromResult(Guid.NewGuid());
        public Task ActivateAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task SeedFromConfigAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static AiTools BuildTools(FakeLocalLlmServer server)
    {
        var provider = new OpenAiCompatibleProvider(new HttpClient(), NullLogger<OpenAiCompatibleProvider>.Instance);
        var client = new RoutingAiClient(new LocalStore(server.BaseUrl), [provider]);
        var feature = new AiFeatureService(client);
        // Text tools never touch the DB/http; an unopened context is enough to satisfy the ctor.
        var db = new DataContext(new DbContextOptionsBuilder<DataContext>().UseNpgsql("Host=unused").Options);
        return new AiTools(db, new HttpContextAccessor(), feature);
    }

    [Fact]
    public async Task Mcp_generate_cbot_tool_returns_local_llm_output()
    {
        using var server = new FakeLocalLlmServer(Reply);
        var result = await BuildTools(server).GenerateCBot("CSharp", "an RSI bot");
        result.ToString().Should().Contain(Reply);
    }

    [Fact]
    public async Task Mcp_review_cbot_tool_returns_local_llm_output()
    {
        using var server = new FakeLocalLlmServer(Reply);
        var result = await BuildTools(server).ReviewCBot("CSharp", "public class Bot {}");
        result.ToString().Should().Contain(Reply);
    }

    [Fact]
    public async Task Mcp_market_sentiment_tool_returns_local_llm_output()
    {
        using var server = new FakeLocalLlmServer(Reply);
        var result = await BuildTools(server).MarketSentiment("EURUSD");
        result.ToString().Should().Contain(Reply);
    }
}
