using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core;
using Core.Agent;
using Core.Execution;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Decisions_ledger_starts_empty_and_is_user_scoped()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new { Name = "Ledger", Archetype = "Scalper" });

        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/agent-studio/{id}/decisions");
        decisions.EnumerateArray().Should().BeEmpty();

        (await client.GetAsync($"/api/agent-studio/{Guid.NewGuid()}/decisions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_approves_a_pending_decision()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new { Name = "Gated", Archetype = "DayTrader", Autonomy = "ApprovalGated" });

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var aid = TradingAgentId.From(id);
            var agent = await db.TradingAgents.FirstAsync(a => a.Id == aid);
            var decision = new AgentDecision("buy signal",
                new AgentOrderIntent(TradingAccountId.New(), "EURUSD", OrderSide.Buy, 1), []);
            var processed = new ProcessedDecision(DecisionOutcome.PendingApproval, "awaiting", false, false, decision.Order);
            db.AgentDecisionRecords.Add(AgentDecisionRecord.Create(agent.Id, agent.UserId, 1, decision, processed));
            await db.SaveChangesAsync();
        }

        var approve = await client.PostAsync($"/api/agent-studio/{id}/decisions/1/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approve.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approval").GetString().Should().Be("Approved");

        // Re-approving an already-approved decision is an invalid transition.
        (await client.PostAsync($"/api/agent-studio/{id}/decisions/1/approve", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approving_a_missing_decision_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new { Name = "X", Archetype = "Scalper" });
        (await client.PostAsync($"/api/agent-studio/{id}/decisions/99/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Debate_returns_a_disabled_result_without_ai()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new { Name = "Desk", Archetype = "SwingTrader" });

        var response = await client.PostAsync($"/api/agent-studio/{id}/debate", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("synthesis").GetString().Should().Contain("not configured");
        body.GetProperty("opinions").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Memory_persists_and_recalls()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateAsync(client, new { Name = "Mem", Archetype = "Scalper" });

        using (var scope = app.Services.CreateScope())
        {
            var memory = scope.ServiceProvider.GetRequiredService<Core.Agent.IAgentMemory>();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var agent = await db.TradingAgents.FirstAsync(a => a.Id == TradingAgentId.From(id));
            await memory.RememberAsync(agent.Id, agent.UserId, Core.Agent.MemoryTier.LowLevelReflection, "held: no signal", default);
        }

        var recalled = await client.GetFromJsonAsync<JsonElement>($"/api/agent-studio/{id}/memory");
        recalled.EnumerateArray().Should().ContainSingle();
        recalled.EnumerateArray().First().GetProperty("content").GetString().Should().Be("held: no signal");
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
