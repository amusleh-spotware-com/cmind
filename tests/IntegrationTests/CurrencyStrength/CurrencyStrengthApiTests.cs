using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Ai;
using Core.Calendar;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests.CurrencyStrength;

/// <summary>
/// End-to-end HTTP coverage of the currency-strength surface against real Postgres: the in-app REST API, the
/// cBot JWT API (reusing the calendar's <c>CalendarJwt</c> + a <c>market:read</c> scope), calendar-anchored
/// assembly, and every degradation/failure path. The AI edge is stubbed (canned forward JSON); the domain
/// math runs for real.
/// </summary>
public class CurrencyStrengthApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@cs.local";
    private const string Password = "Owner_Pass_123!";
    private static readonly DateTimeOffset Effective = new(2026, 6, 15, 13, 30, 0, TimeSpan.Zero);

    private WebApplicationFactory<Program> CreateApp(bool aiEnabled, params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiClient>();
                services.AddScoped<IAiClient>(_ => new StubAiClient(aiEnabled));
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
        await IngestAsync(writer, "US.UNEMP.RATE", MarketMovingCategory.Employment, 4.1m, 4.2m);
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
    public async Task Refresh_then_latest_returns_ranking_and_pair_matrix()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        await SeedCalendarAsync(app);

        var refresh = await owner.PostAsync("/api/ai/currency-strength/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK, await refresh.Content.ReadAsStringAsync());

        var view = await owner.GetFromJsonAsync<JsonElement>("/api/ai/currency-strength/latest?horizon=3M");
        view.GetProperty("ranking").GetArrayLength().Should().BeGreaterThan(0);
        view.GetProperty("pairs").GetArrayLength().Should().BeGreaterThan(0);
        view.GetProperty("source").GetString().Should().Be("CalendarAndAi");
        view.GetProperty("ranking").EnumerateArray().Select(r => r.GetProperty("code").GetString())
            .Should().Contain("USD");
    }

    [Fact]
    public async Task Latest_before_any_refresh_is_no_content()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        (await owner.GetAsync("/api/ai/currency-strength/latest")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Tier_filter_scopes_the_ranking()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        await owner.PostAsync("/api/ai/currency-strength/refresh", null);

        var majors = await owner.GetFromJsonAsync<JsonElement>("/api/ai/currency-strength/latest?tier=Majors");
        majors.GetProperty("ranking").EnumerateArray()
            .Should().OnlyContain(r => r.GetProperty("tier").GetString() == "Major");
    }

    [Fact]
    public async Task History_returns_snapshots()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        await owner.PostAsync("/api/ai/currency-strength/refresh", null);

        var history = await owner.GetFromJsonAsync<JsonElement>("/api/ai/currency-strength/history?days=30");
        history.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Ai_off_with_calendar_yields_a_calendar_only_snapshot()
    {
        await using var app = CreateApp(aiEnabled: false);
        var owner = await LoginAsync(app);
        await SeedCalendarAsync(app);

        var refresh = await owner.PostAsync("/api/ai/currency-strength/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        view.GetProperty("source").GetString().Should().Be("CalendarOnly");
    }

    [Fact]
    public async Task Both_off_produces_no_snapshot()
    {
        await using var app = CreateApp(aiEnabled: false);
        var owner = await LoginAsync(app);
        (await owner.PostAsync("/api/ai/currency-strength/refresh", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Feature_gate_off_404s_the_surface()
    {
        await using var app = CreateApp(aiEnabled: true, ("App:Features:Ai", "false"));
        var owner = await LoginAsync(app);
        (await owner.GetAsync("/api/ai/currency-strength/latest")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Refresh_requires_owner()
    {
        await using var app = CreateApp(aiEnabled: true);
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await anon.PostAsync("/api/ai/currency-strength/refresh", null);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Found, HttpStatusCode.Forbidden);
    }

    // --- cBot JWT surface (reused calendar CalendarJwt + market:read scope) ---

    private static async Task<(string ClientId, string ClientSecret)> IssueClientAsync(HttpClient owner, params string[] scopes)
    {
        var created = await owner.PostAsJsonAsync("/api/calendar/clients",
            new { Name = "cbot", Scopes = scopes, ExpiresInDays = (int?)null });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("clientId").GetString()!, body.GetProperty("clientSecret").GetString()!);
    }

    private static async Task<string> TokenAsync(HttpClient anon, string clientId, string clientSecret)
    {
        var response = await anon.PostAsJsonAsync("/api/calendar/v1/token",
            new { ClientId = clientId, ClientSecret = clientSecret });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Cbot_with_market_scope_reads_the_currency_strength_api()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        await owner.PostAsync("/api/ai/currency-strength/refresh", null);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.MarketRead);

        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var latest = await anon.GetAsync("/api/market/v1/currency-strength/latest?horizon=3M");
        latest.StatusCode.Should().Be(HttpStatusCode.OK);
        var pair = await anon.GetAsync("/api/market/v1/currency-strength/pair/EUR/USD?horizon=3M");
        pair.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cbot_without_market_scope_is_forbidden()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.Read);

        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await anon.GetAsync("/api/market/v1/currency-strength/latest")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cbot_without_a_token_is_unauthorized()
    {
        await using var app = CreateApp(aiEnabled: true);
        var response = await app.CreateClient().GetAsync("/api/market/v1/currency-strength/latest");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cbot_with_a_tampered_token_is_unauthorized()
    {
        await using var app = CreateApp(aiEnabled: true);
        var owner = await LoginAsync(app);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.MarketRead);

        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token + "x");

        (await anon.GetAsync("/api/market/v1/currency-strength/latest")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Deterministic AI edge: returns canned forward JSON for a gather, a canned narrative otherwise.</summary>
    private sealed class StubAiClient(bool enabled) : IAiClient
    {
        public bool Enabled => enabled;

        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
        {
            if (!enabled) return Task.FromResult(AiResult.Fail("disabled"));
            if (request.System.Contains("FORWARD", StringComparison.Ordinal))
                return Task.FromResult(AiResult.Ok("""
                {"currencies":[
                  {"code":"USD","trajectory":{"ratePathBp":-50,"inflationTrend":-0.5,"growthMomentum":0.3,"geopoliticalDelta":1.0},"dataConfidence":"High"},
                  {"code":"EUR","trajectory":{"ratePathBp":0,"inflationTrend":-0.2,"growthMomentum":0.4,"geopoliticalDelta":0.0},"dataConfidence":"High"},
                  {"code":"JPY","trajectory":{"ratePathBp":50,"inflationTrend":0.1,"growthMomentum":0.0,"geopoliticalDelta":0.8},"dataConfidence":"Medium"}
                ]}
                """));
            return Task.FromResult(AiResult.Ok("EUR/USD leans bullish over 3M as the Fed cuts while the ECB holds."));
        }
    }
}
