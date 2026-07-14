using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

// HTTP exercise of the time-zone pipeline against a real app + Postgres: the /set-timezone endpoint's cookie
// + profile persistence, canonicalization of a Windows id to IANA, rejection of an unknown zone, the
// anonymous cookie-only path, and the open-redirect guard. Mirrors LocalizationFlowTests.
public class TimeZoneFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@tz.local";
    private const string Password = "Owner_Pass_123!";
    private const string TimeZoneCookie = ".Cmind.TimeZone";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    private static HttpClient NewClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task LoginAsync(HttpClient client) =>
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task Set_timezone_persists_the_choice_to_the_signed_in_users_profile()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        await LoginAsync(client);

        var set = await client.GetAsync("/set-timezone?tz=America/New_York&redirectUri=/");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/");
        set.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        string.Join(";", cookies!).Should().Contain(TimeZoneCookie)
            .And.Contain(Uri.EscapeDataString("America/New_York"));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant());
        user!.Profile.TimeZone.Should().Be("America/New_York");
    }

    [Fact]
    public async Task Set_timezone_canonicalizes_a_windows_id_to_iana()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        await LoginAsync(client);

        var set = await client.GetAsync($"/set-timezone?tz={Uri.EscapeDataString("GMT Standard Time")}&redirectUri=/");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant());
        user!.Profile.TimeZone.Should().Be("Europe/London", "a Windows id must be stored as its canonical IANA form");
    }

    [Fact]
    public async Task Set_timezone_anonymously_sets_the_cookie_without_a_profile()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        var set = await client.GetAsync("/set-timezone?tz=Asia/Tokyo&redirectUri=/login");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/login");
        set.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        string.Join(";", cookies!).Should().Contain(TimeZoneCookie)
            .And.Contain(Uri.EscapeDataString("Asia/Tokyo"));
    }

    [Fact]
    public async Task Set_timezone_ignores_an_unknown_zone()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        var set = await client.GetAsync("/set-timezone?tz=Not/AZone&redirectUri=/login");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/login");

        var wroteCookie = set.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.Contains(TimeZoneCookie, StringComparison.Ordinal));
        wroteCookie.Should().BeFalse("no cookie is written for a rejected zone");
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("//evil.example.com")]
    [InlineData("http://evil.example.com/path")]
    public async Task Set_timezone_never_redirects_off_site(string hostileRedirect)
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        var set = await client.GetAsync($"/set-timezone?tz=Europe/Paris&redirectUri={Uri.EscapeDataString(hostileRedirect)}");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/", "an off-site redirect target must collapse to the dashboard");
    }
}
