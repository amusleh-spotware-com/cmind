using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core;
using Core.Calendar;
using FluentAssertions;
using Infrastructure.Calendar;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Calendar;

public class CalendarApiHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@calendar.local";
    private const string Password = "Owner_Pass_123!";
    private static readonly DateTimeOffset Effective = new(2024, 2, 13, 13, 30, 0, TimeSpan.Zero);

    private WebApplicationFactory<Program> CreateApp(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            foreach (var (key, value) in settings) b.UseSetting(key, value);
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

    private static async Task SeedEventAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var writer = scope.ServiceProvider.GetRequiredService<CalendarWriteService>();
        var series = await writer.UpsertSeriesAsync(
            new SeriesCode("US.CPI.MoM"), new CountryCode("US"), "US CPI (MoM)",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, 0.85, "FRED", "CPIAUCSL", CancellationToken.None);
        await writer.IngestReleaseAsync(series,
            new SourceReleaseItem("CPIAUCSL", Effective, Effective.AddMinutes(1), 3.1m, 2.9m, "%", "fred"),
            CancellationToken.None);
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

    [Fact]
    public async Task Authenticated_client_reads_events()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedEventAsync(app);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.Read);

        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var events = await anon.GetFromJsonAsync<JsonElement>(
            "/api/calendar/v1/events?from=2024-01-01&to=2025-01-01");
        events.EnumerateArray().Should().Contain(e => e.GetProperty("seriesCode").GetString() == "US.CPI.MOM");
    }

    [Fact]
    public async Task Missing_token_is_unauthorized()
    {
        await using var app = CreateApp();
        var response = await app.CreateClient().GetAsync("/api/calendar/v1/events");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Scope_is_enforced()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.Blackout);

        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // A blackout-only token cannot read events.
        (await anon.GetAsync("/api/calendar/v1/events")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anon.GetAsync("/api/calendar/v1/blackout?symbol=EURUSD")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Bad_client_secret_is_rejected()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        var (clientId, _) = await IssueClientAsync(owner, CalendarScopes.Read);

        var response = await app.CreateClient().PostAsJsonAsync("/api/calendar/v1/token",
            new { ClientId = clientId, ClientSecret = "wrong" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Feature_toggle_off_hard_404s_the_whole_tree()
    {
        await using var app = CreateApp(("App:Features:EconomicCalendar", "false"));
        var anon = app.CreateClient();

        (await anon.GetAsync("/api/calendar/v1/health")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anon.PostAsJsonAsync("/api/calendar/v1/token", new { ClientId = "x", ClientSecret = "y" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Openapi_document_is_served_and_lists_the_events_path()
    {
        await using var app = CreateApp();
        var response = await app.CreateClient().GetAsync("/api/calendar/v1/openapi.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("paths").TryGetProperty("/events", out _).Should().BeTrue();
        doc.GetProperty("openapi").GetString().Should().StartWith("3.");
    }

    [Fact]
    public async Task Batch_multiplexes_several_event_queries()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        await SeedEventAsync(app);
        var (clientId, secret) = await IssueClientAsync(owner, CalendarScopes.Read);

        var anon = app.CreateClient();
        var token = await TokenAsync(anon, clientId, secret);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new[]
        {
            new { from = "2024-01-01", to = "2025-01-01" },
            new { from = "2024-01-01", to = "2025-01-01" }
        };
        var response = await anon.PostAsJsonAsync("/api/calendar/v1/events/batch", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        results.GetArrayLength().Should().Be(2);
        results[0].GetProperty("events").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task White_label_hard_gate_off_404s_the_whole_tree()
    {
        await using var app = CreateApp(("App:Branding:EnableEconomicCalendar", "false"));
        (await app.CreateClient().GetAsync("/api/calendar/v1/health")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
