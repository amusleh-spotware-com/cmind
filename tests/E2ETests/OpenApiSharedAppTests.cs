using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class OpenApiSharedAppTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Owner_sees_shared_app_and_rate_limit_controls_on_desktop_and_mobile()
    {
        foreach (var page in new[] { await app.NewAuthedPageAsync(), await app.NewAuthedMobilePageAsync() })
        {
            await page.GotoAsync("/settings/openapi");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await Assertions.Expect(page.GetByText("Deployment shared application")).ToBeVisibleAsync(Slow);
            await Assertions.Expect(page.GetByText("Client rate limits")).ToBeVisibleAsync(Slow);
            (await page.Locator("#blazor-error-ui:visible").CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task Owner_can_adjust_a_per_category_rate_limit()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            var set = await page.APIRequest.PutAsync($"{app.BaseUrl}/api/openapi/shared/rate-limits",
                new APIRequestContextOptions { DataObject = new { Category = "HistoricalData", Value = 3 } });
            set.Status.Should().Be(200);

            var get = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/openapi/shared/rate-limits");
            get.Status.Should().Be(200);
            (await get.JsonAsync())!.Value.GetProperty("HistoricalData").GetInt32().Should().Be(3);
        }
        finally
        {
            await page.APIRequest.PutAsync($"{app.BaseUrl}/api/openapi/shared/rate-limits",
                new APIRequestContextOptions { DataObject = new { Category = "HistoricalData", Value = 5 } });
        }
    }

    [Fact]
    public async Task Configuring_shared_app_enables_shared_mode_and_blocks_per_user_app()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            var put = await page.APIRequest.PutAsync($"{app.BaseUrl}/api/openapi/shared",
                new APIRequestContextOptions
                {
                    DataObject = new { Name = "Shared", ClientId = "shared-cid", ClientSecret = "shared-secret" }
                });
            put.Status.Should().Be(200);

            var blocked = await page.APIRequest.PutAsync($"{app.BaseUrl}/api/openapi/application",
                new APIRequestContextOptions
                {
                    DataObject = new { Name = "Mine", ClientId = "cid", ClientSecret = "secret" }
                });
            blocked.Status.Should().Be(409);

            var appInfo = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/openapi/application");
            (await appInfo.JsonAsync())!.Value.GetProperty("sharedMode").GetBoolean().Should().BeTrue();
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/openapi/shared");
        }
    }

    [Fact]
    public async Task Non_owner_under_shared_mode_sees_the_managed_panel_not_the_owner_controls()
    {
        var ownerPage = await app.NewAuthedPageAsync();
        var email = $"user-{Guid.NewGuid():N}@e2e.local";
        const string password = "User_Pass_123!";
        const string newPassword = "User_Pass_456!";

        var create = await ownerPage.APIRequest.PostAsync($"{app.BaseUrl}/api/users",
            new APIRequestContextOptions { DataObject = new { Email = email, Password = password, Role = 2 } });
        create.Status.Should().Be(200);
        var putShared = await ownerPage.APIRequest.PutAsync($"{app.BaseUrl}/api/openapi/shared",
            new APIRequestContextOptions
            {
                DataObject = new { Name = "Shared", ClientId = "shared-cid", ClientSecret = "shared-secret" }
            });
        putShared.Status.Should().Be(200);

        var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = app.BaseUrl });
        try
        {
            var login = await context.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/login",
                new APIRequestContextOptions { DataObject = new { Email = email, Password = password } });
            login.Status.Should().Be(200);
            // Clear the first-login must-change-password gate so the settings page renders normally.
            var change = await context.APIRequest.PostAsync($"{app.BaseUrl}/api/auth/change-password",
                new APIRequestContextOptions
                {
                    DataObject = new { CurrentPassword = password, NewPassword = newPassword }
                });
            change.Status.Should().Be(200);

            var page = await context.NewPageAsync();
            await page.GotoAsync("/settings/openapi");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await Assertions.Expect(page.GetByText("Open API is managed by your provider")).ToBeVisibleAsync(Slow);
            (await page.GetByText("Deployment shared application").CountAsync()).Should().Be(0);
            (await page.Locator("#blazor-error-ui:visible").CountAsync()).Should().Be(0);
        }
        finally
        {
            await ownerPage.APIRequest.DeleteAsync($"{app.BaseUrl}/api/openapi/shared");
            await context.DisposeAsync();
        }
    }
}
