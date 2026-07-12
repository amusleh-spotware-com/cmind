using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Core.Constants;
using Core.NodeAgent;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace UnitTests;

public class ExternalNodeAgentTests(AgentFactory factory) : IClassFixture<AgentFactory>
{
    [Fact]
    public async Task Health_is_anonymous()
    {
        var resp = await factory.CreateClient().GetAsync(HealthEndpoints.Health);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_requires_a_token()
    {
        var resp = await factory.CreateClient().GetAsync(NodeAgentRoutes.Report("nonexistent-container"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_rejects_token_signed_with_wrong_secret()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, NodeAgentRoutes.Report("nonexistent-container"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            AgentFactory.MintToken("a-different-secret-that-is-32-chars-x"));
        var resp = await factory.CreateClient().SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_accepts_valid_token()
    {
        var request = AgentFactory.AuthedRequest(HttpMethod.Get, NodeAgentRoutes.Report("nonexistent-container"));
        var resp = await factory.CreateClient().SendAsync(request);
        // Auth + protocol passed; the report for an unknown container is 404.
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Api_rejects_missing_protocol_version()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, NodeAgentRoutes.Report("nonexistent-container"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AgentFactory.MintToken(AgentFactory.Secret));
        var resp = await factory.CreateClient().SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);
    }

    [Fact]
    public async Task Api_rejects_incompatible_protocol_version()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, NodeAgentRoutes.Report("nonexistent-container"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AgentFactory.MintToken(AgentFactory.Secret));
        request.Headers.Add(NodeAgentProtocol.HeaderName, (NodeAgentProtocol.Version + 1).ToString());
        var resp = await factory.CreateClient().SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);
    }

    [Fact]
    public async Task Info_reports_product_and_protocol_version()
    {
        var request = AgentFactory.AuthedRequest(HttpMethod.Get, NodeAgentRoutes.Info);
        var resp = await factory.CreateClient().SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var info = await resp.Content.ReadFromJsonAsync<NodeAgentInfoResponse>();
        info.Should().NotBeNull();
        info!.ProtocolVersion.Should().Be(NodeAgentProtocol.Version);
        info.ProductVersion.Should().NotBeNullOrWhiteSpace();
    }
}

public sealed class AgentFactory : WebApplicationFactory<Program>
{
    public const string Secret = "test-shared-secret-at-least-32-chars-long";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // The agent reads its secret at builder time, so it must be present before the host is built.
        Environment.SetEnvironmentVariable("NodeAgent__JwtSecret", Secret);
        Environment.SetEnvironmentVariable("NodeAgent__DataRoot", Path.Combine(Path.GetTempPath(), "app-agent-test"));
        return base.CreateHost(builder);
    }

    public static HttpRequestMessage AuthedRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(Secret));
        request.Headers.Add(NodeAgentProtocol.HeaderName, NodeAgentProtocol.Version.ToString());
        return request;
    }

    public static string MintToken(string secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            NodeAgentAuth.Issuer, NodeAgentAuth.Audience, null, now, now.AddMinutes(2), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
