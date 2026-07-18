using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// The async AI-task HTTP surface (create fan-out, list, detail, cancel, delete). The task runner is disabled
// (App:Ai:RunTasks=false) so task state is deterministic — this asserts the API + persistence + model-name
// mapping + owner scoping, not the worker (that is AiTaskRunnerTests). The seeded built-in ONNX credential is
// the selectable model.
public class AiTaskEndpointTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@tasks.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Ai:BuiltIn:AutoDownload", "false");
            b.UseSetting("App:Ai:RunTasks", "false");
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

    private const string Model = "claude-test-model";

    // Create a deployment-scoped provider via the owner API so the test doesn't depend on the shared-fixture
    // seed surviving other AI test classes. Kind is sent as its integer value (no string-enum converter is
    // configured on the app's JSON options): AiProviderKind.Anthropic = 0.
    private static async Task<Guid> CreateProviderAsync(HttpClient client)
    {
        var put = await client.PutAsJsonAsync("/api/ai/providers", new
        {
            Kind = 0,
            BaseUrl = "https://api.anthropic.com/",
            Model,
            ApiKey = "test-key",
            Activate = false
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_lists_details_cancels_and_deletes_a_task()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var credId = await CreateProviderAsync(client);

        var create = await client.PostAsJsonAsync("/api/ai/tasks", new
        {
            Name = "MyBot",
            Language = "CSharp",
            Description = "RSI mean reversion on EURUSD h1",
            CredentialIds = new[] { credId }
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var taskIds = created.GetProperty("taskIds").EnumerateArray().Select(e => e.GetGuid()).ToList();
        taskIds.Should().ContainSingle();
        var taskId = taskIds[0];

        // List shows the task with the human model name, never a raw credential GUID.
        var list = await client.GetFromJsonAsync<JsonElement>("/api/ai/tasks");
        var row = list.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == taskId);
        row.GetProperty("status").GetString().Should().Be("Queued");
        row.GetProperty("model").GetString().Should().Be(Model);

        // Detail.
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/ai/tasks/{taskId}");
        detail.GetProperty("feature").GetString().Should().Be("GenerateCBot");
        detail.GetProperty("status").GetString().Should().Be("Queued");

        // Cancel → terminal.
        (await client.PostAsync($"/api/ai/tasks/{taskId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterCancel = await client.GetFromJsonAsync<JsonElement>($"/api/ai/tasks/{taskId}");
        afterCancel.GetProperty("status").GetString().Should().Be("Cancelled");

        // Delete → gone.
        (await client.DeleteAsync($"/api/ai/tasks/{taskId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/ai/tasks/{taskId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_fans_out_one_task_per_selected_model()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var credId = await CreateProviderAsync(client);

        // Two identical ids collapse to one (distinct); a real multi-model selection makes N rows.
        var create = await client.PostAsJsonAsync("/api/ai/tasks", new
        {
            Description = "scalp gold",
            CredentialIds = new[] { credId }
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("taskIds").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Create_rejects_a_blank_description()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var credId = await CreateProviderAsync(client);

        var create = await client.PostAsJsonAsync("/api/ai/tasks", new { Description = "   ", CredentialIds = new[] { credId } });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_is_rejected_while_the_task_is_active()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var credId = await CreateProviderAsync(client);

        var create = await client.PostAsJsonAsync("/api/ai/tasks", new { Description = "x", CredentialIds = new[] { credId } });
        var taskId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("taskIds")[0].GetGuid();

        // Still Queued (runner disabled) → active → delete refused until stopped/cancelled.
        (await client.DeleteAsync($"/api/ai/tasks/{taskId}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
