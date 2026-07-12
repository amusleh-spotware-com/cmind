using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// End-to-end HTTP exercise of self-service registration against a real app + Postgres: the disabled-by-
// default gate, admin-approval and email-verification-downgrade flows, provisioning API, anti-enumeration,
// and the abuse guards (domain allow-list, disposable email, weak password, required attributes).
public class RegistrationFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@reg.local";
    private const string OwnerPassword = "Owner_Pass_123!";
    private const string ProvisionSecret = "provision-secret-abcdefghijklmnop";

    private WebApplicationFactory<Program> CreateApp(Action<IWebHostBuilder>? extra = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", OwnerPassword);
            extra?.Invoke(b);
        });

    private static void EnableRegistration(IWebHostBuilder b, string mode = "AdminApproval")
    {
        b.UseSetting("App:Features:Registration", "true");
        b.UseSetting("App:Registration:Enabled", "true");
        b.UseSetting("App:Registration:Mode", mode);
        b.UseSetting("App:Registration:RequireTermsAcceptance", "false");
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static object Register(string email, string password = "Str0ngPass!", bool acceptTerms = false,
        string? country = null)
        => new { Email = email, Password = password, AcceptTerms = acceptTerms, Country = country };

    private static async Task<HttpClient> OwnerClientAsync(WebApplicationFactory<Program> app)
    {
        var client = NewClient(app);
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password = OwnerPassword }))
            .EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Registration_disabled_by_default_returns_404()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        (await client.PostAsJsonAsync("/api/register", Register("nope@reg.local")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/register/config")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_approval_flow_blocks_login_until_approved()
    {
        await using var app = CreateApp(b => EnableRegistration(b));
        var client = NewClient(app);

        var reg = await client.PostAsJsonAsync("/api/register", Register("pending@reg.local"));
        reg.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await reg.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString()
            .Should().Be("pending_approval");

        // Cannot log in while pending.
        (await client.PostAsJsonAsync("/api/auth/login",
                new { Email = "pending@reg.local", Password = "Str0ngPass!" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Owner approves.
        // The fixture DB is shared across tests, so match our specific pending user by email.
        var owner = await OwnerClientAsync(app);
        var pending = await (await owner.GetAsync("/api/users/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var id = pending.EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "pending@reg.local")
            .GetProperty("id").GetString();
        (await owner.PostAsJsonAsync($"/api/users/{id}/approve", new { })).EnsureSuccessStatusCode();

        // Now login works.
        (await client.PostAsJsonAsync("/api/auth/login",
                new { Email = "pending@reg.local", Password = "Str0ngPass!" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Email_verification_mode_downgrades_to_approval_without_mail()
    {
        await using var app = CreateApp(b => EnableRegistration(b, mode: "EmailVerification"));
        var client = NewClient(app);

        // No SMTP configured, so the effective mode is admin approval.
        var cfg = await (await client.GetAsync("/api/register/config")).Content.ReadFromJsonAsync<JsonElement>();
        cfg.GetProperty("mode").GetString().Should().Be("AdminApproval");

        var reg = await client.PostAsJsonAsync("/api/register", Register("verify@reg.local"));
        (await reg.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString()
            .Should().Be("pending_approval");
    }

    [Fact]
    public async Task Duplicate_email_is_neutral_and_creates_one_user()
    {
        await using var app = CreateApp(b => EnableRegistration(b));
        var client = NewClient(app);

        (await client.PostAsJsonAsync("/api/register", Register("dupe@reg.local")))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        // Second attempt gets the SAME neutral response — no enumeration.
        (await client.PostAsJsonAsync("/api/register", Register("dupe@reg.local")))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var owner = await OwnerClientAsync(app);
        var users = await (await owner.GetAsync("/api/users/")).Content.ReadFromJsonAsync<JsonElement>();
        users.EnumerateArray().Count(u =>
                u.GetProperty("email").GetString() == "dupe@reg.local")
            .Should().Be(1);
    }

    [Fact]
    public async Task Abuse_guards_reject_bad_input()
    {
        await using var app = CreateApp(b =>
        {
            EnableRegistration(b);
            b.UseSetting("App:Registration:AllowedEmailDomains:0", "allowed.com");
        });
        var client = NewClient(app);

        // Domain not on the allow-list.
        (await client.PostAsJsonAsync("/api/register", Register("user@other.com")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // Disposable provider.
        (await client.PostAsJsonAsync("/api/register", Register("user@mailinator.com")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // Weak password.
        (await client.PostAsJsonAsync("/api/register", Register("user@allowed.com", password: "short")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // Allowed domain + strong password succeeds.
        (await client.PostAsJsonAsync("/api/register", Register("good@allowed.com")))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Required_attribute_is_enforced()
    {
        await using var app = CreateApp(b =>
        {
            EnableRegistration(b);
            b.UseSetting("App:Registration:Attributes:Country", "Required");
        });
        var client = NewClient(app);

        (await client.PostAsJsonAsync("/api/register", Register("nocountry@reg.local")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/register", Register("withcountry@reg.local", country: "US")))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Provisioning_api_creates_active_user_and_rejects_bad_secret()
    {
        await using var app = CreateApp(b =>
        {
            EnableRegistration(b);
            b.UseSetting("App:Registration:Api:Enabled", "true");
            b.UseSetting("App:Registration:Api:Secret", ProvisionSecret);
        });
        var client = NewClient(app);

        // Bad secret rejected.
        var bad = new HttpRequestMessage(HttpMethod.Post, "/api/provision")
        {
            Content = JsonContent.Create(new { Email = "svc@reg.local", Password = "Str0ngPass!" })
        };
        bad.Headers.Add("X-Provision-Secret", "wrong");
        (await client.SendAsync(bad)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Correct secret provisions an immediately-active user.
        var ok = new HttpRequestMessage(HttpMethod.Post, "/api/provision")
        {
            Content = JsonContent.Create(new { Email = "svc@reg.local", Password = "Str0ngPass!" })
        };
        ok.Headers.Add("X-Provision-Secret", ProvisionSecret);
        (await client.SendAsync(ok)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The provisioned user can log in right away (active, no approval needed).
        (await client.PostAsJsonAsync("/api/auth/login",
                new { Email = "svc@reg.local", Password = "Str0ngPass!" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
