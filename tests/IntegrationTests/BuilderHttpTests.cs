using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the cBot source-builder endpoints over the real app + Postgres: project CRUD +
// template files, and (Docker) a real container build of the default C# template. (Coverage backfill.)
public class BuilderHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@builder.local";
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

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string name)
    {
        var create = await client.PostAsJsonAsync("/api/builder/projects", new { Name = name, Language = 0 });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Project_crud_and_template_files()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateProjectAsync(client, "MyProject");

        var project = await (await client.GetAsync($"/api/builder/projects/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        project.GetProperty("language").GetString().Should().Be("CSharp");

        var files = await (await client.GetAsync($"/api/builder/projects/{id}/files")).Content.ReadFromJsonAsync<JsonElement>();
        files.GetProperty("files").EnumerateObject().Should().NotBeEmpty("a new project is seeded with template files");

        (await client.PutAsJsonAsync($"/api/builder/projects/{id}/files",
            new { Files = new Dictionary<string, string> { ["Robot.cs"] = "// edited" } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listed = await (await client.GetAsync("/api/builder/projects")).Content.ReadFromJsonAsync<JsonElement>();
        listed.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Should().Contain(id);

        (await client.DeleteAsync($"/api/builder/projects/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Build_the_default_template_in_a_container()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var id = await CreateProjectAsync(client, "BuildMe");

        // Real container build of the seeded C# template — the default template compiles.
        var build = await client.PostAsJsonAsync($"/api/builder/projects/{id}/build", new { });
        build.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await build.Content.ReadFromJsonAsync<JsonElement>();
        result.TryGetProperty("success", out var success).Should().BeTrue("the build reports a success flag");
        success.GetBoolean().Should().BeTrue("the seeded default C# template compiles cleanly");
    }
}
