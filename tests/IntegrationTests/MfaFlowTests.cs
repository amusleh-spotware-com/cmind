using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OtpNet;
using Xunit;

namespace IntegrationTests;

// End-to-end HTTP exercise of the two-factor flow against a real app + Postgres: enrollment, the two-step
// login challenge (TOTP and backup code), and the white-label mandatory-enrollment gate. TOTP codes are
// computed with the same RFC 6238 primitives the server verifies with.
public class MfaFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@mfa.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(bool requireMfa = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            if (requireMfa) b.UseSetting("App:Branding:RequireMfa", "true");
        });

    private static HttpClient NewClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // The app verifies against the real system clock, so a live timestamp is required (a hardcoded
    // time would be rejected). Read it via TimeProvider, not DateTime.UtcNow, per the clock mandate.
    private static string Compute(string secret) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(TimeProvider.System.GetUtcNow().UtcDateTime);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resp)
        => await resp.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Full_two_factor_lifecycle_totp_and_backup_code()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        // Password-only login (no MFA yet) succeeds outright.
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(login)).GetProperty("mfaSetupRequired").GetBoolean().Should().BeFalse();

        // Enroll: fetch a secret, confirm with a live code.
        var setup = await JsonAsync(await client.PostAsync("/api/auth/mfa/setup", null));
        var secret = setup.GetProperty("secret").GetString()!;
        setup.GetProperty("qrSvg").GetString().Should().Contain("<svg");

        var confirm = await client.PostAsJsonAsync("/api/auth/mfa/confirm", new { Code = Compute(secret) });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        var backupCodes = (await JsonAsync(confirm)).GetProperty("backupCodes")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        backupCodes.Should().HaveCount(10);

        (await client.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();

        // Fresh login now stops at the second factor — the auth cookie is NOT issued yet.
        var client2 = NewClient(app);
        var challenge = await client2.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password });
        challenge.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(challenge)).GetProperty("mfaRequired").GetBoolean().Should().BeTrue();
        (await client2.GetAsync("/api/auth/mfa/status")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "not fully signed in until the second factor clears");

        // Wrong code is rejected; correct TOTP completes sign-in.
        (await client2.PostAsJsonAsync("/api/auth/login/verify-2fa", new { Code = "000000" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client2.PostAsJsonAsync("/api/auth/login/verify-2fa", new { Code = Compute(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client2.GetAsync("/api/auth/mfa/status")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A backup code also satisfies the challenge, exactly once.
        (await client2.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        var client3 = NewClient(app);
        (await client3.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .EnsureSuccessStatusCode();
        (await client3.PostAsJsonAsync("/api/auth/login/verify-2fa", new { Code = backupCodes[0] }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Same backup code is now spent.
        (await client3.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        var client4 = NewClient(app);
        (await client4.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password }))
            .EnsureSuccessStatusCode();
        (await client4.PostAsJsonAsync("/api/auth/login/verify-2fa", new { Code = backupCodes[0] }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mandatory_mfa_forces_enrollment_after_password_step()
    {
        await using var app = CreateApp(requireMfa: true);
        var client = NewClient(app);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(login)).GetProperty("mfaSetupRequired").GetBoolean()
            .Should().BeTrue("branding requires MFA and the owner has none");

        // A page navigation is redirected to the enrollment page until MFA is set up.
        var page = new HttpRequestMessage(HttpMethod.Get, "/");
        page.Headers.Add("Accept", "text/html");
        var pageResp = await client.SendAsync(page);
        pageResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        pageResp.Headers.Location!.ToString().Should().Be("/account");

        // The enrollment page itself and API calls are not gated.
        var account = new HttpRequestMessage(HttpMethod.Get, "/account");
        account.Headers.Add("Accept", "text/html");
        (await client.SendAsync(account)).StatusCode.Should().NotBe(HttpStatusCode.Redirect);
    }
}
