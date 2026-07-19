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

// The AI Build project chat persists the conversation (the user's prompt + the model's reply, ordered by
// time) and applies the model's source to the project — over the real app + Postgres with a stubbed AI edge.
public class AiBuildProjectHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@aibuild.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Authoring", "true");
            b.UseSetting("App:Features:Ai", "true");
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiClient>();
                services.AddScoped<IAiClient, CannedCodeAiClient>();
            });
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Prompt_persists_the_user_turn_and_the_background_reply_in_order()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var create = await client.PostAsJsonAsync("/api/builder/projects", new { Name = "ChatBot", Language = 0 });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var before = await (await client.GetAsync($"/api/ai/build/{id}/messages")).Content.ReadFromJsonAsync<JsonElement>();
        before.GetArrayLength().Should().Be(0);

        // The prompt is accepted immediately and generation runs detached from the request.
        var prompt = await client.PostAsJsonAsync($"/api/ai/build/{id}/prompt", new { Prompt = "RSI mean reversion on EURUSD" });
        prompt.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Poll until both turns are persisted (the background task finishes shortly).
        List<JsonElement> messages = [];
        for (var attempt = 0; attempt < 40 && messages.Count < 2; attempt++)
        {
            await Task.Delay(250);
            var list = await (await client.GetAsync($"/api/ai/build/{id}/messages")).Content.ReadFromJsonAsync<JsonElement>();
            messages = list.EnumerateArray().ToList();
        }

        messages.Should().HaveCount(2);
        messages[0].GetProperty("role").GetString().Should().Be("User");
        messages[0].GetProperty("content").GetString().Should().Be("RSI mean reversion on EURUSD");
        messages[1].GetProperty("role").GetString().Should().Be("Assistant");
        messages[1].GetProperty("content").GetString().Should().Contain("Robot");

        // Activity clears once the reply lands.
        var status = await (await client.GetAsync($"/api/ai/build/{id}/status")).Content.ReadFromJsonAsync<JsonElement>();
        status.GetProperty("working").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Messages_and_prompt_for_a_missing_project_are_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var ghost = Guid.NewGuid();
        (await client.GetAsync($"/api/ai/build/{ghost}/messages")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/ai/build/{ghost}/prompt", new { Prompt = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class CannedCodeAiClient : IAiClient
    {
        public bool Enabled => true;

        public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct) =>
            Task.FromResult(AiResult.Ok("```csharp\npublic class Robot {}\n```"));
    }
}
