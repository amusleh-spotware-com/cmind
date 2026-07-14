extern alias mcp;
using System.Net;
using System.Net.Http.Json;
using Core.Ai;
using Core.Calendar;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using AiTools = mcp::Mcp.Tools.AiTools;

namespace IntegrationTests;

// F19 — the MCP currency-strength tool returns the latest persisted snapshot (ranking + pairs + AI
// narrative) that a refresh produced. Own PostgresFixture (fresh DB) so the global snapshot this test
// writes never perturbs the shared CurrencyStrengthApiTests' ordering-sensitive "before any refresh"
// assertion. Drive a refresh, then invoke the MCP AiTools.CurrencyStrength and assert it surfaces the
// AI-authored narrative — the MCP surface's coverage for this data-backed tool.
public sealed class McpCurrencyStrengthLocalLlmTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@mcp-cs.local";
    private const string Password = "Owner_Pass_123!";
    private const string Narrative = "EUR/USD leans bullish over 3M as the Fed cuts while the ECB holds.";
    private static readonly DateTimeOffset Effective = new(2026, 6, 15, 13, 30, 0, TimeSpan.Zero);

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiClient>();
                services.AddScoped<IAiClient>(_ => new StubAiClient());
            });
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private static async Task SeedCalendarAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<CalendarWriteService>();
        await IngestAsync(writer, "US.POLICY.RATE", MarketMovingCategory.InterestRate, 5.25m, 5.20m);
        await IngestAsync(writer, "US.CPI.YOY", MarketMovingCategory.Inflation, 3.1m, 3.3m);
        await IngestAsync(writer, "US.GDP.QOQ", MarketMovingCategory.Growth, 2.4m, 2.0m);
    }

    private static async Task IngestAsync(
        CalendarWriteService writer, string code, MarketMovingCategory category, decimal actual, decimal forecast)
    {
        var series = await writer.UpsertSeriesAsync(
            new SeriesCode(code), new CountryCode("US"), code, category, ReleaseCadence.Monthly, 0.85, "FRED", code, CancellationToken.None);
        await writer.IngestReleaseAsync(series,
            new SourceReleaseItem(code, Effective, Effective.AddMinutes(1), actual, forecast, "%", "fred"), CancellationToken.None);
    }

    [Fact]
    public async Task Mcp_currency_strength_tool_returns_the_ai_narrative_snapshot()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedCalendarAsync(app);
        (await owner.PostAsync("/api/ai/currency-strength/refresh", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = app.Services.CreateScope();
        var tools = new AiTools(
            scope.ServiceProvider.GetRequiredService<DataContext>(),
            new HttpContextAccessor(),
            scope.ServiceProvider.GetRequiredService<IAiFeatureService>(),
            scope.ServiceProvider.GetRequiredService<Core.Ai.CurrencyStrength.ICurrencyStrengthQuery>());

        var result = await tools.CurrencyStrength("3M", null);
        result.ToString().Should().Contain("EUR/USD leans bullish",
            "the MCP tool must surface the AI narrative from the refreshed snapshot");
    }

    // Deterministic AI edge: canned forward JSON for the gather, the canned narrative otherwise.
    private sealed class StubAiClient : IAiClient
    {
        public bool Enabled => true;

        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
        {
            if (request.System.Contains("FORWARD", StringComparison.Ordinal))
                return Task.FromResult(AiResult.Ok("""
                {"currencies":[
                  {"code":"USD","trajectory":{"ratePathBp":-50,"inflationTrend":-0.5,"growthMomentum":0.3,"geopoliticalDelta":1.0},"dataConfidence":"High"},
                  {"code":"EUR","trajectory":{"ratePathBp":0,"inflationTrend":-0.2,"growthMomentum":0.4,"geopoliticalDelta":0.0},"dataConfidence":"High"},
                  {"code":"JPY","trajectory":{"ratePathBp":50,"inflationTrend":0.1,"growthMomentum":0.0,"geopoliticalDelta":0.8},"dataConfidence":"Medium"}
                ]}
                """));
            return Task.FromResult(AiResult.Ok(Narrative));
        }
    }
}
