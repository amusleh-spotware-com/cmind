using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests.Calendar;

public class EconomicAlertHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@ecoalert.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        return client;
    }

    [Fact]
    public async Task Create_economic_event_alert_persists_and_lists()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);
        var name = $"CPI watch {Guid.NewGuid():N}";

        var create = await owner.PostAsJsonAsync("/api/alerts/rules/economic-event",
            new { name, minImpact = "High", minutesBefore = 30, currencies = "USD,EUR", intervalMinutes = 15 });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var rules = await owner.GetFromJsonAsync<JsonElement>("/api/alerts/rules");
        rules.EnumerateArray().Should().Contain(r => r.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Create_economic_event_alert_without_a_name_is_rejected()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app);

        var create = await owner.PostAsJsonAsync("/api/alerts/rules/economic-event",
            new { name = "", minImpact = "High", minutesBefore = 30, currencies = (string?)null });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
