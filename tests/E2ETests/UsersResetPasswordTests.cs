using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Regression for A-07: resetting a user's password must surface the generated temporary password in a
// persistent dialog with a copy affordance — not a one-shot auto-dismissing snackbar that loses the
// credential if the admin blinks. The dialog stays open until explicitly dismissed.
[Collection(AppCollection.Name)]
public sealed class UsersResetPasswordTests(AppFixture app)
{
    [Fact]
    public async Task Reset_password_shows_temp_password_in_a_persistent_dialog_with_copy()
    {
        var email = $"reset_{Guid.NewGuid():N}@e2e.local";

        // Seed a regular user to reset (never reset the shared owner).
        var owner = await app.NewAuthedPageAsync();
        var create = await owner.APIRequest.PostAsync($"{app.BaseUrl}/api/users/", new APIRequestContextOptions
        {
            DataObject = new { Email = email, Password = "Temp_Pass_123!", Role = 2 },
        });
        create.Ok.Should().BeTrue($"create user failed: {create.Status} {await create.TextAsync()}");
        JsonDocument.Parse(await create.TextAsync()).RootElement.GetProperty("id").GetString().Should().NotBeNull();

        await owner.GotoAsync($"{app.BaseUrl}/users", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Click Reset on the seeded user's row.
        var row = owner.Locator("tr", new() { HasText = email });
        await row.Locator("[aria-label='Reset password']").ClickAsync();

        // A dialog (not merely a snackbar) shows the temp password and a copy button, and persists.
        var dialog = owner.Locator(".mud-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await Assertions.Expect(owner.Locator("[data-testid=temp-password]")).ToBeVisibleAsync();
        var tempInput = owner.Locator(".mud-dialog input").First;
        await Assertions.Expect(tempInput).ToBeVisibleAsync();
        (await tempInput.InputValueAsync()).Should().NotBeNullOrWhiteSpace("the generated temp password is shown");

        // It stays open (no auto-dismiss) — still visible after a beat.
        await owner.WaitForTimeoutAsync(1200);
        await Assertions.Expect(dialog).ToBeVisibleAsync();

        // Copy affordance is present, and the dialog only closes when dismissed.
        (await owner.Locator(".mud-dialog button:has-text('Copy'), .mud-dialog [aria-label*='Copy']").CountAsync())
            .Should().BeGreaterThan(0, "a copy affordance is offered for the sensitive credential");
    }
}
