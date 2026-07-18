extern alias mcp;
using System.Security.Claims;
using Core;
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

// The MCP AnalyzeBacktest tool reads a real completed-backtest report from the DB (scoped to the caller)
// before it calls the AI, so — unlike the text-only tools — it needs a real database and an authenticated
// caller. This seeds a completed backtest for a user, points the tool at the in-process fake local LLM,
// and asserts the tool returns the model's reply: the MCP surface's AI E2E for the data-backed tool.
public sealed class McpAnalyzeBacktestLocalLlmTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Reply = "MCP-ANALYZE-LLM-OK";
    private static readonly DateTimeOffset Now = TestClock.Now;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(new FixedTimeProvider(Now)))
            .Options);

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
        public Task<IReadOnlyList<AiProviderView>> ListForUserAsync(UserId user, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiProviderView>>([]);
        public Task<Guid> UpsertForUserAsync(UserId user, UpsertAiProviderCommand command, CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());
        public Task ActivateForUserAsync(UserId user, Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveForUserAsync(UserId user, Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task SeedFromConfigAsync(CancellationToken ct) => Task.CompletedTask;
        public ActiveAiProvider? ResolveFor(AiFeature? feature, AiProviderCredentialId? credentialId) => Active;
        public Task<IReadOnlyList<AiFeatureBindingView>> ListBindingsAsync(UserId? owner, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiFeatureBindingView>>([]);
        public Task SetBindingAsync(UserId? owner, AiFeature feature, AiProviderCredentialId credentialId, CancellationToken ct) =>
            Task.CompletedTask;
        public Task ClearBindingAsync(UserId? owner, AiFeature feature, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NullCurrencyStrengthQuery : Core.Ai.CurrencyStrength.ICurrencyStrengthQuery
    {
        public Task<Core.Ai.CurrencyStrength.CurrencyStrengthView?> LatestAsync(
            Core.Ai.CurrencyStrength.Horizon horizon, string? tierFilter, CancellationToken ct) =>
            Task.FromResult<Core.Ai.CurrencyStrength.CurrencyStrengthView?>(null);

        public Task<Core.Ai.CurrencyStrength.PairRow?> PairAsync(
            string @base, string quote, Core.Ai.CurrencyStrength.Horizon horizon, CancellationToken ct) =>
            Task.FromResult<Core.Ai.CurrencyStrength.PairRow?>(null);

        public Task<IReadOnlyList<Core.Ai.CurrencyStrength.StrengthHistoryPoint>> HistoryAsync(
            int days, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Core.Ai.CurrencyStrength.StrengthHistoryPoint>>([]);
    }

    private AiTools BuildTools(FakeLocalLlmServer server, DataContext db, UserId uid)
    {
        var provider = new OpenAiCompatibleProvider(new HttpClient(), NullLogger<OpenAiCompatibleProvider>.Instance);
        var client = new RoutingAiClient(new LocalStore(server.BaseUrl), [provider]);
        var feature = new AiFeatureService(client);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, uid.Value.ToString())]))
        };
        var http = new HttpContextAccessor { HttpContext = context };
        return new AiTools(db, http, feature, new NullCurrencyStrengthQuery());
    }

    [Fact]
    public async Task Mcp_analyze_backtest_tool_returns_local_llm_output_for_seeded_report()
    {
        UserId uid;
        Guid instanceId;
        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
            var user = OwnerUser.Create(new Email($"mcp-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());
            var cbot = CBot.Create(user.Id, $"bot-{Guid.NewGuid():N}", []);
            var node = LocalNode.Create($"node-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
            var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(
                    user.Id, cbot.Id, node.Id, new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("H1"), null))
                .ToRunning("c1", Now.AddMinutes(-30))
                .ToCompleted(Now.AddMinutes(-5), """{"netProfit":100.0,"maxDrawdown":10.0,"totalTrades":42}""");
            completed.ClearDomainEvents();

            setup.Users.Add(user);
            setup.CBots.Add(cbot);
            setup.Nodes.Add(node);
            setup.Instances.Add(completed);
            await setup.SaveChangesAsync();

            uid = user.Id;
            instanceId = completed.Id.Value;
        }

        using var server = new FakeLocalLlmServer(Reply);
        await using var db = CreateContext();
        var result = await BuildTools(server, db, uid).AnalyzeBacktest(instanceId);
        result.ToString().Should().Contain(Reply);
    }
}
