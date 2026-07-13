using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the cBot authoring endpoints over the real app + Postgres: multipart upload
// (algo encrypted at rest), list, rename, and the owner-scoped not-found. (Coverage backfill.)
public class CBotHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@cbot.local";
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

    [Fact]
    public async Task Upload_list_and_rename_a_cbot()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3, 4]), "file", "bot.algo" },
            { new StringContent("Scalper"), "name" },
        };
        var upload = await client.PostAsync("/api/cbots/upload", form);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await (await client.GetAsync("/api/cbots/")).Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Contain("Scalper");

        var rename = await client.PatchAsJsonAsync($"/api/cbots/{id}", new { Name = "Breakout" });
        rename.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await (await client.GetAsync("/api/cbots/")).Content.ReadFromJsonAsync<JsonElement>();
        after.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id)
            .GetProperty("name").GetString().Should().Be("Breakout");
    }

    [Fact]
    public async Task Renaming_a_missing_cbot_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PatchAsJsonAsync($"/api/cbots/{Guid.NewGuid()}", new { Name = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_rejects_an_empty_file()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([]), "file", "empty.algo" },
            { new StringContent("Empty"), "name" },
        };
        (await client.PostAsync("/api/cbots/upload", form)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
