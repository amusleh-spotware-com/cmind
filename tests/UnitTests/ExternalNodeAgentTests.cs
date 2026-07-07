using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Core.Constants;
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
        var request = new HttpRequestMessage(HttpMethod.Get, NodeAgentRoutes.Report("nonexistent-container"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AgentFactory.MintToken(AgentFactory.Secret));
        var resp = await factory.CreateClient().SendAsync(request);
        // Auth passed; the report for an unknown container is 404.
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public sealed class AgentFactory : WebApplicationFactory<Program>
{
    public const string Secret = "test-shared-secret-at-least-32-chars-long";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // The agent reads its secret at builder time, so it must be present before the host is built.
        Environment.SetEnvironmentVariable("NodeAgent__JwtSecret", Secret);
        Environment.SetEnvironmentVariable("NodeAgent__DataRoot", Path.Combine(Path.GetTempPath(), "ctw-agent-test"));
        return base.CreateHost(builder);
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
