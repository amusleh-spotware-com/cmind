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
}
