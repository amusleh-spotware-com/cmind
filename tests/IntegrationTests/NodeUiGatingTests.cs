using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// The white-label Nodes gate over real HTTP + Postgres: the NodesUi mode strips manual add/delete
// (Monitor) or removes the manual API entirely (Hidden), and RestrictNodesToOwner floors the surface
// at owner-only so an admin is forbidden.
public class NodeUiGatingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@nodes.local";
    private const string OwnerPassword = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", OwnerPassword);
            foreach (var (key, value) in settings) b.UseSetting(key, value);
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string email, string password)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private static object NodeBody(string name) => new
    {
        Name = name,
        BaseUrl = "http://10.20.30.40:8080",
        ApiSecret = new string('x', 32),
        Mode = "Run",
        DataDirPath = "/var/lib/app/data",
        MaxInstances = 5
    };

    [Fact]
    public async Task Full_mode_allows_manual_add()
    {
        await using var app = CreateApp();
        var owner = await LoginAsync(app, Owner, OwnerPassword);

        var created = await owner.PostAsJsonAsync("/api/nodes/", NodeBody("full-mode-node"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Monitor_mode_hides_manual_add_and_delete()
    {
        await using var app = CreateApp(("App:Branding:NodesUi", "Monitor"));
        var owner = await LoginAsync(app, Owner, OwnerPassword);

        // The list stays readable for monitoring...
        (await owner.GetAsync("/api/nodes/")).StatusCode.Should().Be(HttpStatusCode.OK);
        // ...but manual add and delete are gone.
        (await owner.PostAsJsonAsync("/api/nodes/", NodeBody("monitor-mode-node")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.DeleteAsync($"/api/nodes/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hidden_mode_removes_manual_add()
    {
        await using var app = CreateApp(("App:Branding:NodesUi", "Hidden"));
        var owner = await LoginAsync(app, Owner, OwnerPassword);

        (await owner.PostAsJsonAsync("/api/nodes/", NodeBody("hidden-mode-node")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restrict_to_owner_forbids_admin_but_allows_owner()
    {
        await using var app = CreateApp(("App:Branding:RestrictNodesToOwner", "true"));
        var owner = await LoginAsync(app, Owner, OwnerPassword);

        // Owner mints an admin, who then cannot reach the owner-only node surface.
        const string adminEmail = "admin@nodes.local";
        const string adminPassword = "Admin_Pass_123!";
        var create = await owner.PostAsJsonAsync("/api/users/",
            new { Email = adminEmail, Password = adminPassword, Role = 1 });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await LoginAsync(app, adminEmail, adminPassword);
        (await admin.GetAsync("/api/nodes/")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The owner still sees them.
        (await owner.GetAsync("/api/nodes/")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
