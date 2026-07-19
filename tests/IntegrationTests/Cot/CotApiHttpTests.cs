using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Calendar;
using Core.Cot;
using Infrastructure.Cot;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FluentAssertions;
using Xunit;

namespace IntegrationTests.Cot;

public class CotApiHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@cot.local";
    private const string Password = "Owner_Pass_123!";
    private const string Code = "099741";

    private WebApplicationFactory<Program> CreateApp(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            // Keep the weekly ingestion worker off in tests — we seed deterministically.
            b.UseSetting("App:Cot:IngestionEnabled", "false");
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            // Replace the live CFTC source with an empty fake so the read-through cache never hits the network;
            // these tests seed the database directly and assert it is served from there.
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<Core.Cot.ICotSource>();
                s.AddSingleton<Core.Cot.ICotSource>(new FakeCotSource());
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

    private static async Task SeedAsync(WebApplicationFactory<Program> app, int weeks = 3)
    {
        using var scope = app.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<CotWriteService>();
        for (var i = 0; i < weeks; i++)
        {
            var reportDate = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero).AddDays(7 * i);
            var report = new CotSourceReport(
                Code, "Euro FX", "CHICAGO MERCANTILE EXCHANGE", CotReportKind.Legacy, false,
                reportDate, 700000, -1500,
                [
                    new CotSourceCategory(CotTraderCategory.NonCommercial, 200000 + 10000 * i, 120000, 30000, null, null),
                    new CotSourceCategory(CotTraderCategory.Commercial, 300000, 350000, 0, null, null),
                    new CotSourceCategory(CotTraderCategory.NonReportable, 40000, 50000, 0, null, null)
                ]);
            await writer.IngestAsync(report, CancellationToken.None);
        }
    }

    private static async Task<(string ClientId, string ClientSecret)> IssueClientAsync(
        HttpClient owner, params string[] scopes)
    {
        var created = await owner.PostAsJsonAsync("/api/calendar/clients",
            new { Name = "cbot", Scopes = scopes, ExpiresInDays = (int?)null });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
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

    private static async Task<HttpClient> CBotClientAsync(WebApplicationFactory<Program> app, HttpClient owner)
    {
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.MarketRead);
        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return anon;
    }

    [Fact]
    public async Task Persistence_upserts_market_and_appends_reports_idempotently()
    {
        await using var app = CreateApp();
        await SeedAsync(app);
        await SeedAsync(app); // re-ingest same weeks — must be a no-op

        using var scope = app.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<ICotReports>();
        var markets = await reads.GetMarketsAsync(null, null, CancellationToken.None);
        markets.Should().Contain(m => m.ContractCode == Code);

        var latest = await reads.GetLatestAsync(new ContractMarketCode(Code), CotReportKind.Legacy, false, null, CancellationToken.None);
        latest.Should().NotBeNull();
        latest!.CotIndex.Should().NotBeNull(); // 3 weeks of history → index computable
        latest.Categories.Should().HaveCount(3);
    }

    [Fact]
    public async Task CBot_reads_latest_with_market_scope()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedAsync(app);
        var cbot = await CBotClientAsync(app, owner);

        var response = await cbot.GetAsync($"/api/market/v1/cot/latest?code={Code}&kind=Legacy");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        view.GetProperty("contractCode").GetString().Should().Be(Code);
    }

    [Fact]
    public async Task CBot_reads_history_and_markets()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedAsync(app);
        var cbot = await CBotClientAsync(app, owner);

        var history = await cbot.GetFromJsonAsync<JsonElement>($"/api/market/v1/cot/history/{Code}?kind=Legacy");
        history.GetArrayLength().Should().BeGreaterThan(0);

        var markets = await cbot.GetFromJsonAsync<JsonElement>("/api/market/v1/cot/markets");
        markets.EnumerateArray().Should().Contain(m => m.GetProperty("contractCode").GetString() == Code);
    }

    [Fact]
    public async Task Export_csv_returns_csv_for_the_market()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedAsync(app);

        var response = await owner.GetAsync($"/api/cot/export.csv?code={Code}&kind=Legacy");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith("report_date,open_interest,cot_index,speculator_net,");
        csv.Should().Contain("NonCommercial_net");
    }

    [Fact]
    public async Task Missing_token_is_unauthorized()
    {
        await using var app = CreateApp();
        (await app.CreateClient().GetAsync($"/api/market/v1/cot/latest?code={Code}")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_scope_is_forbidden()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.Blackout);
        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await anon.GetAsync($"/api/market/v1/cot/latest?code={Code}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task In_app_markets_require_authentication()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedAsync(app);

        var markets = await owner.GetFromJsonAsync<JsonElement>("/api/cot/markets");
        markets.EnumerateArray().Should().Contain(m => m.GetProperty("contractCode").GetString() == Code);

        // Anonymous is redirected/denied — never a 200.
        (await app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/api/cot/markets")).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Feature_toggle_off_404s_the_tree()
    {
        await using var app = CreateApp(("App:Features:Cot", "false"));
        (await app.CreateClient().GetAsync($"/api/market/v1/cot/latest?code={Code}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task White_label_hard_gate_off_404s_the_tree()
    {
        await using var app = CreateApp(("App:Branding:EnableCot", "false"));
        var owner = await LoginAsync(app);
        (await owner.GetAsync("/api/cot/markets")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
