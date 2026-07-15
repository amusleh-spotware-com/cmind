using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// The code editor's Run action posts the chosen trading account + parameter set to
// /api/builder/projects/{id}/quick-run. Ownership of both is validated BEFORE the (docker) build runs, so
// these guard paths are exercisable without a node or the console image. (Coverage for the editor Run
// dialog wiring.)
public class BuilderQuickRunHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@quickrun.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Authoring", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/builder/projects",
            new { Name = $"quickrun-{Guid.NewGuid():N}", Language = 0 });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Quick_run_rejects_a_trading_account_the_caller_does_not_own()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var projectId = await CreateProjectAsync(client);

        var res = await client.PostAsJsonAsync($"/api/builder/projects/{projectId}/quick-run",
            new { TradingAccountId = Guid.NewGuid(), ParamSetId = (Guid?)null, Symbol = "EURUSD", Timeframe = "h1" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, "an unknown/foreign trading account is a bad request");
    }

    [Fact]
    public async Task Quick_run_rejects_a_param_set_the_caller_does_not_own()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var projectId = await CreateProjectAsync(client);

        var res = await client.PostAsJsonAsync($"/api/builder/projects/{projectId}/quick-run",
            new { TradingAccountId = (Guid?)null, ParamSetId = Guid.NewGuid(), Symbol = "EURUSD", Timeframe = "h1" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, "an unknown/foreign parameter set is a bad request");
    }

    [Fact]
    public async Task Quick_run_on_a_missing_project_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var res = await client.PostAsJsonAsync($"/api/builder/projects/{Guid.NewGuid()}/quick-run",
            new { TradingAccountId = (Guid?)null, ParamSetId = (Guid?)null, Symbol = "EURUSD", Timeframe = "h1" });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
