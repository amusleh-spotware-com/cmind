using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

// WS-4 launch gate (public-launch-readiness.md): no API endpoint is accidentally public. Every mapped
// /api/* route must declare its intent — either it requires authorization (IAuthorizeData, e.g. a group
// RequireAuthorization or a JWT scheme) or it is explicitly anonymous (IAllowAnonymous) or it is in the
// small, reviewed KnownAnonymous allow-list below. An endpoint with none of those is a latent auth leak
// and fails the build, forcing an explicit decision when a new endpoint is added.
public class EndpointAuthCoverageTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Reviewed public surface: pre-auth entry points. Each carries its own protection (rate limiting, a
    // provisioning secret header, anti-enumeration, TOTP) rather than cookie user authorization.
    private static readonly HashSet<string> KnownAnonymous = new(StringComparer.OrdinalIgnoreCase)
    {
        "api/auth/login",
        "api/auth/login/verify-2fa",
        "api/auth/logout",
        "api/register",
        "api/register/",
        "api/register/config",
        "api/register/resend",
        "api/register/verify",
        "api/provision",
    };

    // Reviewed versioned public APIs secured by a custom JWT endpoint filter (Bearer token issued by the
    // group's own /token endpoint against client credentials), not by cookie IAuthorizeData metadata. The
    // /token issuer and /openapi.json + /health probes on these groups are intentionally reachable.
    private static readonly string[] JwtFilterProtectedPrefixes =
    [
        "api/calendar/v1/",
        "api/market/v1/",
    ];

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
        });

    [Fact]
    public void Every_api_endpoint_declares_authorization_intent()
    {
        using var app = CreateApp();
        using var scope = app.Services.CreateScope();
        var endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints;

        var leaks = endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Route: e.RoutePattern.RawText ?? "", e.Metadata))
            .Where(x => Normalize(x.Route).StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .Where(x => !KnownAnonymous.Contains(Normalize(x.Route)))
            .Where(x => !JwtFilterProtectedPrefixes.Any(p =>
                Normalize(x.Route).StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x.Metadata.GetMetadata<IAuthorizeData>() is null
                     && x.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(x => Normalize(x.Route))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        leaks.Should().BeEmpty(
            "every /api endpoint must require authorization, be explicitly [AllowAnonymous], or be in the "
            + "reviewed KnownAnonymous list. These declare no auth intent (potential public leak):\n  {0}",
            string.Join("\n  ", leaks));
    }

    private static string Normalize(string route) => route.TrimStart('/');
}
