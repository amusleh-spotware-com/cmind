using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Ai;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests;

// Single-shot AI runs (Review / Debate) persist as history, run detached from the request, and expose a
// status the UI polls — over the real app + Postgres with a stubbed AI edge.
public class AiRunHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@airun.local";
    private const string Password = "Owner_Pass_123!";
    private const int ReviewCBot = (int)AiFeature.ReviewCBot;

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Ai", "true");
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiClient>();
                services.AddScoped<IAiClient, CannedReviewAiClient>();
            });
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Review_run_is_accepted_persisted_and_completes_in_background()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var start = await client.PostAsJsonAsync("/api/ai/runs",
            new { Feature = ReviewCBot, Title = "My review", Language = "CSharp", Source = "public class Bot { }" });
        start.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var id = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // It appears in the feature's history immediately.
        var list = await (await client.GetAsync("/api/ai/runs?feature=ReviewCBot")).Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).Should().Contain(id);

        // Poll the detail until the background run completes with output.
        JsonElement detail = default;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            detail = await (await client.GetAsync($"/api/ai/runs/{id}")).Content.ReadFromJsonAsync<JsonElement>();
            if (detail.GetProperty("status").GetString() == "Completed") break;
            await Task.Delay(250);
        }
        detail.GetProperty("status").GetString().Should().Be("Completed");
        detail.GetProperty("output").GetString().Should().Contain("no issues");
        detail.GetProperty("title").GetString().Should().Be("My review");

        (await client.DeleteAsync($"/api/ai/runs/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/ai/runs/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_run_for_another_user_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        (await client.GetAsync($"/api/ai/runs/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class CannedReviewAiClient : IAiClient
    {
        public bool Enabled => true;

        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct) =>
            Task.FromResult(AiResult.Ok("low - no issues found - ship it"));
    }
}
