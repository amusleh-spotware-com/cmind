using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// The built-in ONNX local model install-state endpoints that Settings → AI surfaces so the owner can see
// whether the model is present / downloading / failed and trigger the one-time background download.
// AutoDownload is turned OFF so the test never reaches out to HuggingFace — install stays a no-op and the
// state is deterministic.
public class AiBuiltInEndpointTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@builtin.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Ai:BuiltIn:AutoDownload", "false");
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

    [Fact]
    public async Task Status_reports_not_installed_without_a_model()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var status = await client.GetFromJsonAsync<JsonElement>("/api/ai/built-in/status");
        status.GetProperty("installed").GetBoolean().Should().BeFalse();
        status.GetProperty("state").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Install_is_a_no_op_when_auto_download_is_off()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var install = await client.PostAsync("/api/ai/built-in/install", null);
        install.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await install.Content.ReadFromJsonAsync<JsonElement>();
        // With auto-download disabled the trigger cannot start a download, so it stays not-installed.
        body.GetProperty("installed").GetBoolean().Should().BeFalse();
        body.GetProperty("state").GetString().Should().Be("NotStarted");
    }
}
