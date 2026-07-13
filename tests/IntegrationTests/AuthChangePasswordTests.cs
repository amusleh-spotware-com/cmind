using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the change-password endpoint over the real app + Postgres: the current password is
// verified, the new one takes effect, and a wrong current is rejected. (Coverage backfill.)
public class AuthChangePasswordTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@changepw.local";
    private const string Password = "Owner_Pass_123!";
    private const string NewPassword = "New_Pass_456!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string password)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password = password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Change_password_takes_effect_for_the_next_login()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app, Password);

        (await client.PostAsJsonAsync("/api/auth/change-password",
            new { CurrentPassword = Password, NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var fresh = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await fresh.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await fresh.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the old password no longer works");
    }

    [Fact]
    public async Task Change_password_rejects_a_wrong_current_password()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app, Password);

        (await client.PostAsJsonAsync("/api/auth/change-password",
            new { CurrentPassword = "not-the-password", NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Change_password_requires_authentication()
    {
        await using var app = CreateApp();
        var anon = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await anon.PostAsJsonAsync("/api/auth/change-password",
            new { CurrentPassword = Password, NewPassword }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }
}
