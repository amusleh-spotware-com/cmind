using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class AgentStudioHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@agents.local";
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
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var create = await client.PostAsJsonAsync("/api/agent-studio", body);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_persists_and_lists_the_agent()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var id = await CreateAsync(client, new { Name = "Scalp One", Archetype = "Scalper", Autonomy = "Advisory" });

        var list = await client.GetFromJsonAsync<JsonElement>("/api/agent-studio");
        list.EnumerateArray().Should().Contain(a => a.GetProperty("id").GetGuid() == id);
        var agent = list.EnumerateArray().First(a => a.GetProperty("id").GetGuid() == id);
        agent.GetProperty("status").GetString().Should().Be("Draft");
        agent.GetProperty("archetype").GetString().Should().Be("Scalper");
    }

    [Fact]
    public async Task Start_without_accounts_is_rejected()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new { Name = "No Accounts", Archetype = "DayTrader" });

        var start = await client.PostAsync($"/api/agent-studio/{id}/start", null);
        start.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Full_lifecycle_start_stop_halt()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new
        {
            Name = "Swing",
            Archetype = "SwingTrader",
            Autonomy = "Advisory",
            AccountIds = new[] { Guid.NewGuid() }
        });

        (await client.PostAsync($"/api/agent-studio/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var running = await client.GetFromJsonAsync<JsonElement>($"/api/agent-studio/{id}");
        running.GetProperty("status").GetString().Should().Be("Running");
        running.GetProperty("systemPrompt").GetString().Should().Contain("SwingTrader");

        (await client.PostAsync($"/api/agent-studio/{id}/stop", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var stopped = await client.GetFromJsonAsync<JsonElement>($"/api/agent-studio/{id}");
        stopped.GetProperty("status").GetString().Should().Be("Stopped");

        (await client.DeleteAsync($"/api/agent-studio/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/agent-studio/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task White_label_can_disable_agent_studio()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:AgentStudio", "false");
        });
        var client = await LoginAsync(app);

        (await client.GetAsync("/api/agent-studio")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Full_auto_creation_requires_envelope()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var bad = await client.PostAsJsonAsync("/api/agent-studio", new
        {
            Name = "Auto",
            Archetype = "Scalper",
            Autonomy = "FullAuto",
            AccountIds = new[] { Guid.NewGuid() }
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest); // no envelope -> SetAutonomy(FullAuto) throws
    }
}
