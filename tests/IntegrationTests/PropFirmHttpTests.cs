using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.PropFirm;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the prop-firm challenge endpoints over the real app + Postgres: template presets,
// challenge list, create against an owned account, and the feature gate. (Coverage backfill.)
public class PropFirmHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@propfirm.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(bool enabled = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:PropFirm", enabled ? "true" : "false");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> SeedAccountAsync(HttpClient client)
    {
        var username = $"pf-{Guid.NewGuid():N}";
        var accountNumber = Random.Shared.NextInt64(1_000_000, 9_000_000);
        (await client.PostAsJsonAsync("/api/ctids/", new { Username = username, Password = "p" }))
            .EnsureSuccessStatusCode();
        var cids = await (await client.GetAsync("/api/ctids/")).Content.ReadFromJsonAsync<JsonElement>();
        var cidId = cids.EnumerateArray()
            .First(c => c.GetProperty("username").GetString() == username).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/ctids/{cidId}/accounts",
            new { AccountNumber = accountNumber, Broker = "Pepperstone", IsLive = false, Label = "demo" }))
            .EnsureSuccessStatusCode();
        var accounts = await (await client.GetAsync($"/api/ctids/{cidId}/accounts")).Content.ReadFromJsonAsync<JsonElement>();
        return accounts.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Templates_list_the_presets()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var templates = await (await client.GetAsync("/api/prop-firm/templates")).Content.ReadFromJsonAsync<JsonElement>();
        templates.EnumerateArray().Should().HaveCountGreaterThanOrEqualTo(4, "the challenge presets are listed");
    }

    [Fact]
    public async Task Create_a_challenge_against_an_owned_account()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var accountId = await SeedAccountAsync(client);

        var create = await client.PostAsJsonAsync("/api/prop-firm/challenges", new
        {
            Name = "Two-phase eval",
            TradingAccountId = accountId,
            StartingBalance = 100000m,
            ProfitTargetPercent = 8.0,
            MaxDailyLossPercent = 5.0,
            MaxTotalDrawdownPercent = 10.0,
            DrawdownMode = DrawdownMode.Static,
            MinTradingDays = 3,
            SingleStep = false,
            Kind = ChallengeKind.TwoPhase,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await (await client.GetAsync("/api/prop-firm/challenges")).Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Contain(id);
    }

    [Fact]
    public async Task Detail_endpoint_returns_breach_cause_for_a_failed_challenge()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var accountId = await SeedAccountAsync(client);

        var create = await client.PostAsJsonAsync("/api/prop-firm/challenges", new
        {
            Name = "Breach eval",
            TradingAccountId = accountId,
            StartingBalance = 100000m,
            ProfitTargetPercent = 50.0,
            MaxDailyLossPercent = 5.0,
            MaxTotalDrawdownPercent = 10.0,
            DrawdownMode = DrawdownMode.Static,
            MinTradingDays = 0,
            SingleStep = true,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Drive a daily-loss breach.
        var breach = await client.PostAsJsonAsync($"/api/prop-firm/challenges/{id}/equity", new { Equity = 94000m });
        breach.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await (await client.GetAsync($"/api/prop-firm/challenges/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("Failed");
        detail.GetProperty("breach").GetString().Should().Be("DailyLoss");
    }

    [Fact]
    public async Task Detail_endpoint_returns_not_found_for_a_missing_challenge()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.GetAsync($"/api/prop-firm/challenges/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_are_gated_off_when_the_feature_is_disabled()
    {
        await using var app = CreateApp(enabled: false);
        var client = await LoginAsync(app);

        (await client.GetAsync("/api/prop-firm/challenges")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
