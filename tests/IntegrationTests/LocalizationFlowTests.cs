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

// End-to-end HTTP exercise of the localization pipeline against a real app + Postgres: the request-culture
// resolution order (cookie > Accept-Language > default), RTL emission in the document, the /set-culture
// endpoint's cookie + profile persistence, and its rejection of unsupported cultures.
public class LocalizationFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@loc.local";
    private const string Password = "Owner_Pass_123!";
    private const string CultureCookie = ".AspNetCore.Culture";

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

    private static async Task<string> GetLoginHtmlAsync(HttpClient client, string? acceptLanguage)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/login");
        req.Headers.Add("Accept", "text/html");
        if (acceptLanguage is not null) req.Headers.Add("Accept-Language", acceptLanguage);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resp.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Accept_language_drives_the_document_culture_and_rtl_direction()
    {
        await using var app = CreateApp();

        var arabic = await GetLoginHtmlAsync(NewClient(app), "ar");
        arabic.Should().Contain("lang=\"ar\"").And.Contain("dir=\"rtl\"");

        var german = await GetLoginHtmlAsync(NewClient(app), "de-DE,de;q=0.9");
        german.Should().Contain("lang=\"de\"").And.Contain("dir=\"ltr\"");

        // An unknown language falls back to the English default, never to a raw/blank culture.
        var fallback = await GetLoginHtmlAsync(NewClient(app), "xx");
        fallback.Should().Contain("lang=\"en\"").And.Contain("dir=\"ltr\"");
    }

    [Fact]
    public async Task Culture_cookie_overrides_the_browser_accept_language()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        // Set the cookie via the real endpoint, then a page request with a conflicting Accept-Language.
        var set = await client.GetAsync("/set-culture?culture=de&redirectUri=/login");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await GetLoginHtmlAsync(client, "fr");
        html.Should().Contain("lang=\"de\"", "the culture cookie must win over Accept-Language");
    }

    [Fact]
    public async Task Set_culture_persists_the_choice_to_the_signed_in_users_profile()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var set = await client.GetAsync("/set-culture?culture=ja&redirectUri=/");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/");
        set.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        string.Join(";", cookies!).Should().Contain(CultureCookie).And.Contain("ja");

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == Owner.ToUpperInvariant());
        user.Should().NotBeNull();
        user!.Profile.Locale.Should().Be("ja");
    }

    [Fact]
    public async Task Set_culture_ignores_an_unsupported_culture()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        var set = await client.GetAsync("/set-culture?culture=klingon&redirectUri=/login");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/login");

        // No culture cookie is written for a rejected culture.
        var wroteCookie = set.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.Contains(CultureCookie, StringComparison.Ordinal));
        wroteCookie.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("//evil.example.com")]
    [InlineData("http://evil.example.com/path")]
    public async Task Set_culture_never_redirects_off_site(string hostileRedirect)
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        var set = await client.GetAsync($"/set-culture?culture=de&redirectUri={Uri.EscapeDataString(hostileRedirect)}");
        set.StatusCode.Should().Be(HttpStatusCode.Redirect);
        set.Headers.Location!.ToString().Should().Be("/", "an off-site redirect target must collapse to the dashboard");
    }
}
