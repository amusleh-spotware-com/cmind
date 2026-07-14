using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Xunit;

namespace AspireTests;

/// <summary>
/// Automated smoke for the .NET Aspire deployment (<c>src/AppHost</c>). Boots the real distributed
/// application through <see cref="DistributedApplicationTestingBuilder"/> — Postgres + Web + MCP — and
/// asserts the Web resource reaches healthy and serves. Unlike a manual <c>dotnet run</c> this harness
/// allocates endpoints dynamically and hands back a wired <see cref="HttpClient"/>, so it does not hit the
/// fixed-port DCP proxy collision a developer sees locally. Guards the AppHost orchestration wiring
/// (resource graph, parameters, project references, the Web→appdb dependency) that no other tier covers.
/// </summary>
public sealed class AspireAppHostSmokeTests
{
    [Fact]
    public async Task AppHost_boots_and_web_becomes_healthy_and_serves()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(cts.Token);

        // Supply the values the AppHost's AddParameter calls require (no interactive prompt in a test).
        builder.Configuration["Parameters:OwnerEmail"] = "owner@aspire.local";
        builder.Configuration["Parameters:OwnerPassword"] = "Aspire_Owner_Str0ng!1";
        builder.Configuration["Parameters:PgPassword"] = "Aspire_Pg_Str0ng!1";
        // The data-protection cert is optional in Development (ephemeral keys); an empty value is still a
        // provided value, so the parameter resolves without prompting.
        builder.Configuration["Parameters:DataProtectionCertBase64"] = "";
        builder.Configuration["Parameters:DataProtectionCertPassword"] = "";

        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        // Web waits on appdb (WaitFor) and runs migrations + owner seeding before its readiness passes.
        await app.ResourceNotifications.WaitForResourceHealthyAsync("web", cts.Token);

        // Target the plain-http endpoint by name: the default endpoint is https, whose ASP.NET dev cert is
        // untrusted on a CI runner (UntrustedRoot) and would fail the smoke's own request even though the
        // resource is healthy. The smoke only needs to reach the app, not exercise TLS.
        var http = app.CreateHttpClient("web", "http");

        var health = await http.GetAsync("/health", cts.Token);
        health.StatusCode.Should().Be(HttpStatusCode.OK, "the Web resource must report healthy under Aspire wiring");

        var version = await http.GetAsync("/version", cts.Token);
        version.StatusCode.Should().Be(HttpStatusCode.OK);
        (await version.Content.ReadAsStringAsync(cts.Token)).Should().Contain("productVersion");
    }
}
