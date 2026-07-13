using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Regression for A-08: a user whose password was reset (MustChangePassword=true) must be forced to the
// account page and blocked from roaming the app until they set a new password. Before the fix the flag
// was modeled and returned by the API but never enforced in the UI/middleware.
[Collection(AppCollection.Name)]
public sealed class MustChangePasswordTests(AppFixture app)
{
    [Fact]
    public async Task Reset_password_user_is_confined_to_account_until_they_change_it()
    {
        var email = $"mustchange_{Guid.NewGuid():N}@e2e.local";
        const string tempPassword = "Temp_Pass_123!";

        // As the owner, create a regular user and reset their password → MustChangePassword=true.
        var owner = await app.NewAuthedPageAsync();
        var create = await owner.APIRequest.PostAsync($"{app.BaseUrl}/api/users/", new APIRequestContextOptions
        {
            DataObject = new { Email = email, Password = tempPassword, Role = 2 },
        });
        create.Ok.Should().BeTrue($"create user failed: {create.Status} {await create.TextAsync()}");
        var id = JsonDocument.Parse(await create.TextAsync()).RootElement.GetProperty("id").GetString();

        var reset = await owner.APIRequest.PostAsync($"{app.BaseUrl}/api/users/{id}/reset-password", new APIRequestContextOptions
        {
            DataObject = new { NewPassword = tempPassword },
        });
        reset.Ok.Should().BeTrue($"reset failed: {reset.Status} {await reset.TextAsync()}");

        // Log in as that user in a clean context, then try to reach a normal page.
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync($"{app.BaseUrl}/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name=Email]", email);
        await page.FillAsync("input[name=Password]", tempPassword);
        await page.ClickAsync("#app-login-submit");
        await page.WaitForURLAsync($"{app.BaseUrl}/**", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Landed on /account (forced) and cannot navigate away until password changed.
        await page.GotoAsync($"{app.BaseUrl}/cbots", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        page.Url.Should().EndWith("/account", "a must-change-password user must be redirected back to /account");
    }
}
