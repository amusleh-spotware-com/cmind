using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// "Copy Invite Link" copies the per-user Open API authorize URL to the clipboard. Shared-app model: this
// is a NON-owner control (the owner manages the deployment shared app instead), so the test acts as a
// regular user who configured their own application, clicks the button, and verifies the URL lands on the
// clipboard.
[Collection(AppCollection.Name)]
public sealed class OpenApiInviteTests(AppFixture app)
{
    [Fact]
    public async Task Copy_invite_link_copies_url_to_clipboard()
    {
        var email = $"invite_{Guid.NewGuid():N}@e2e.local";
        const string tempPassword = "User_Pass_123!";
        const string newPassword = "User_Pass_456!";

        // Owner creates a regular user (created with a temp password → MustChangePassword).
        var owner = await app.NewAuthedPageAsync();
        (await owner.APIRequest.PostAsync($"{app.BaseUrl}/api/users/",
            new() { DataObject = new { Email = email, Password = tempPassword, Role = 2 } }))
            .Ok.Should().BeTrue();

        // Sign in as that user in a clean context, then clear the forced password change so they can roam.
        var context = await app.Browser.NewContextAsync(new() { BaseURL = app.BaseUrl });
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = app.BaseUrl });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name=Email]", email);
        await page.FillAsync("input[name=Password]", tempPassword);
        await page.ClickAsync("#app-login-submit");
        await page.WaitForURLAsync(u => !u.Contains("/login", StringComparison.Ordinal), new() { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/change-password",
            new() { DataObject = new { CurrentPassword = tempPassword, NewPassword = newPassword } }))
            .Ok.Should().BeTrue("change-password clears MustChangePassword so the user can navigate freely");

        // As the regular user, configure their own Open API application → the configured view shows the
        // Copy Invite Link control.
        (await page.APIRequest.PutAsync($"{app.BaseUrl}/api/openapi/application",
            new() { DataObject = new { Name = "E2E OpenAPI App", ClientId = "12345_e2e", ClientSecret = "secret_e2e_abcdef" } }))
            .Ok.Should().BeTrue();

        await page.GotoAsync("/settings/openapi", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var copyBtn = page.GetByRole(AriaRole.Button, new() { Name = "Copy Invite Link" });
        await copyBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await copyBtn.ClickAsync();

        await page.GetByText("Invite link copied to clipboard").WaitForAsync(new() { Timeout = 8000 });

        var clip = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        clip.Should().NotBeNullOrWhiteSpace("the invite URL should be on the clipboard");

        await context.DisposeAsync();
    }
}
