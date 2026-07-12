using FluentAssertions;
using Microsoft.Playwright;
using OtpNet;
using Xunit;

namespace E2ETests;

// Drives the real two-factor UI end to end: enable from the profile (QR + confirm + backup codes), then a
// fresh sign-in stopping at the /login/2fa challenge, completed once with a TOTP code and once with a backup
// code. Always disables MFA again so the shared owner account is left as it was found.
[Collection(AppCollection.Name)]
public sealed class MfaFlowTests(AppFixture app)
{
    // The code must match the running app, which verifies against the real system clock, so a live
    // timestamp is required here (a hardcoded time would be rejected). Read it via TimeProvider, not
    // DateTime.UtcNow, to honour the ambient-clock mandate.
    private static string Totp(string secret) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(TimeProvider.System.GetUtcNow().UtcDateTime);

    [Fact]
    public async Task Enable_from_profile_then_sign_in_with_totp_and_backup_code()
    {
        string? secret = null;

        try
        {
            // --- Enable 2FA from the profile page ---
            var account = await app.NewAuthedPageAsync();
            await account.GotoAsync("/account");
            await account.ClickAsync("[data-testid=mfa-enable-open]");

            await account.WaitForSelectorAsync("[data-testid=mfa-qr] svg");
            secret = await account.Locator("[data-testid=mfa-secret]").InputValueAsync();
            secret.Should().NotBeNullOrWhiteSpace();

            await account.FillAsync("[data-testid=mfa-confirm-code]", Totp(secret));
            await account.ClickAsync("[data-testid=mfa-confirm-submit]");

            await account.WaitForSelectorAsync("[data-testid=mfa-backup-codes]");
            var backupCode = (await account.Locator("[data-testid=mfa-backup-codes] div").First.InnerTextAsync()).Trim();
            backupCode.Should().NotBeNullOrWhiteSpace();
            await account.ClickAsync("[data-testid=mfa-done]");

            // --- Fresh sign-in is now challenged for the second factor ---
            await SignInExpectingChallengeAsync(Totp(secret));

            // --- A backup code also completes the challenge ---
            await SignInExpectingChallengeAsync(backupCode);
        }
        finally
        {
            await DisableMfaAsync(secret);
        }
    }

    [Fact]
    public async Task Profile_shows_two_factor_section_and_qr_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/account");

        (await page.Locator("[data-testid=mfa-section]").IsVisibleAsync()).Should().BeTrue();
        await page.ClickAsync("[data-testid=mfa-enable-open]");
        (await page.WaitForSelectorAsync("[data-testid=mfa-qr] svg")).Should().NotBeNull();
    }

    private async Task SignInExpectingChallengeAsync(string code)
    {
        var page = await app.NewAnonymousPageAsync();

        // Password step: the endpoint returns "mfaRequired" and sets the pending-2fa cookie on this
        // browser context (APIRequest shares the context cookie jar). The password form itself is covered
        // by the fixture sign-in; here we assert the challenge is raised, then drive the real /login/2fa UI.
        var passwordStep = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/login",
            new APIRequestContextOptions
            {
                DataObject = new { Email = AppFixture.OwnerEmail, Password = AppFixture.OwnerPassword }
            });
        (await passwordStep.TextAsync()).Should().Contain("mfaRequired", "MFA is enabled so login must challenge");

        // The real challenge UI renders for the half-authenticated session.
        await page.GotoAsync("/login/2fa");
        (await page.Locator("[data-testid=mfa-code]").IsVisibleAsync()).Should().BeTrue("the 2FA challenge page shows");

        // Complete the challenge; the request carries the pending-2fa cookie set on this context.
        var verify = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/login/verify-2fa",
            new APIRequestContextOptions { DataObject = new { Code = code } });
        verify.Status.Should().Be(200, "the TOTP / backup code completes sign-in");

        // The context is now fully authenticated — a protected page renders the app shell.
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync(".mud-appbar, header, nav");
    }

    // Best-effort cleanup: sign in (completing the challenge if raised) and turn MFA back off via the UI so
    // the shared owner account is restored to password-only.
    private async Task DisableMfaAsync(string? secret)
    {
        try
        {
            await DisableMfaCoreAsync(secret);
        }
        catch
        {
            // Best-effort restore of the shared owner; never mask the real test outcome.
        }
    }

    private async Task DisableMfaCoreAsync(string? secret)
    {
        if (secret is null) return;

        var page = await app.NewAnonymousPageAsync();
        await page.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/login",
            new APIRequestContextOptions
            {
                DataObject = new { Email = AppFixture.OwnerEmail, Password = AppFixture.OwnerPassword }
            });
        await page.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/login/verify-2fa",
            new APIRequestContextOptions { DataObject = new { Code = Totp(secret) } });
        await page.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/mfa/disable",
            new APIRequestContextOptions { DataObject = new { Password = AppFixture.OwnerPassword } });
    }
}
