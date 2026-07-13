using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for node self-registration over the real app + Postgres: a valid join-token + protocol
// version upserts the node; a wrong token 401s, a protocol mismatch 426s, and it 404s when discovery is
// off. (Coverage backfill — integration tier + node trust boundary.)
public class NodeRegisterHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@noderegister.local";
    private const string Password = "Owner_Pass_123!";
    private const string JoinToken = "a-long-enough-join-token-1234567890";

    private WebApplicationFactory<Program> CreateApp(bool discovery = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            if (discovery)
            {
                b.UseSetting("App:Discovery:Enabled", "true");
                b.UseSetting("App:Discovery:JoinToken", JoinToken);
            }
        });

    private static object RegisterBody(int protocolVersion = 0, string name = "agent-1") =>
        new
        {
            Name = name,
            BaseUrl = "http://agent-1:8080",
            Mode = "Run",
            MaxInstances = 5,
            DataDirPath = "/data",
            ProtocolVersion = protocolVersion == 0 ? Core.Constants.NodeAgentProtocol.Version : protocolVersion,
        };

    private static HttpRequestMessage Register(object body, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/nodes/register") { Content = JsonContent.Create(body) };
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    [Fact]
    public async Task Valid_join_token_registers_the_node()
    {
        await using var app = CreateApp();
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await anon.SendAsync(Register(RegisterBody(), JoinToken));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // The node now shows in the owner's node list.
        var owner = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await owner.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        var nodes = await (await owner.GetAsync("/api/nodes/")).Content.ReadFromJsonAsync<JsonElement>();
        nodes.EnumerateArray().Select(n => n.GetProperty("name").GetString()).Should().Contain("agent-1");
    }

    [Fact]
    public async Task Wrong_join_token_is_unauthorized()
    {
        await using var app = CreateApp();
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await anon.SendAsync(Register(RegisterBody(), "wrong-token"))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protocol_mismatch_requires_upgrade()
    {
        await using var app = CreateApp();
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await anon.SendAsync(Register(RegisterBody(protocolVersion: Core.Constants.NodeAgentProtocol.Version + 1), JoinToken))).StatusCode
            .Should().Be(HttpStatusCode.UpgradeRequired);
    }

    [Fact]
    public async Task Registration_is_not_found_when_discovery_is_disabled()
    {
        await using var app = CreateApp(discovery: false);
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await anon.SendAsync(Register(RegisterBody(), JoinToken))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}
