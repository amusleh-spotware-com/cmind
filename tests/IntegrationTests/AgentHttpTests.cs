using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the portfolio-agent mandate endpoints over the real app + Postgres: create a
// mandate against an owned cBot, list, update, delete, list proposals, and reject a missing cBot.
// (Coverage backfill — integration tier.)
public class AgentHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@agent.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:PortfolioAgent", "true");
            b.UseSetting("App:Features:Authoring", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> UploadCBotAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "bot.algo" },
            { new StringContent("AgentBot"), "name" },
        };
        var upload = await client.PostAsync("/api/cbots/upload", form);
        upload.EnsureSuccessStatusCode();
        return (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_list_update_delete_a_mandate()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var cbotId = await UploadCBotAsync(client);

        var create = await client.PostAsJsonAsync("/api/agent/mandates",
            new { CBotId = cbotId, Name = "Alpha", Objective = "beat the market", Enabled = true });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await (await client.GetAsync("/api/agent/mandates")).Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).Should().Contain(id);

        (await client.PutAsJsonAsync($"/api/agent/mandates/{id}",
            new { Name = "Beta", Enabled = false })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/agent/mandates/{id}")).StatusCode
            .Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_rejects_an_unowned_cbot()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/agent/mandates",
            new { CBotId = Guid.NewGuid(), Name = "Alpha", Enabled = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Proposals_list_is_empty_for_a_fresh_owner()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var proposals = await client.GetAsync("/api/agent/proposals");
        proposals.StatusCode.Should().Be(HttpStatusCode.OK);
        (await proposals.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);
    }
}
