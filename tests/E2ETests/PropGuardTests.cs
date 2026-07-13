using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class PropGuardTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Prop_guard_page_renders_and_lists_load()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/prop-guard");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.GetByText("New rule")).ToBeVisibleAsync(Slow);
        // /api/prop/rules returned 200 and bound an empty result.
        await Assertions.Expect(page.GetByText("No rules yet.")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task New_rule_toolbar_button_opens_a_dialog()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/prop-guard");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // D-01: creating a rule opens a MudBlazor dialog, not an inline page form.
        (await page.Locator(".mud-dialog").CountAsync()).Should().Be(0, "no dialog is open before clicking New rule");
        await page.ClickAsync("button:has-text('New rule')");
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Help_tip_is_present()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/prop-guard");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // D-08: the page carries a HelpTip.
        await Assertions.Expect(page.Locator("[data-testid=help-tip]").First).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Delete_rule_asks_for_confirmation_and_no_overflow_with_data_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        var accountId = await SeedAccountAsync(page);
        var ruleId = await SeedRuleAsync(page, accountId);

        try
        {
            await page.GotoAsync("/prop-guard");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await Assertions.Expect(page.GetByText("E2E guard rule").First).ToBeVisibleAsync(Slow);

            // D-09: with a rule present, the responsive table must not overflow at 360px.
            var overflow = await page.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
            overflow.Should().BeFalse("the prop-guard rules table must be responsive with data present");

            // D-06: deleting a rule opens a confirm dialog before the DELETE fires.
            await page.GetByLabel("Delete rule").First.ClickAsync();
            await Assertions.Expect(page.Locator("[data-testid=confirm-accept]")).ToBeVisibleAsync(Slow);
            await page.ClickAsync("[data-testid=confirm-cancel]");

            var list = await GetJsonAsync(page, "/api/prop/rules");
            list.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).Should().Contain(ruleId,
                "cancelling the confirm dialog must not delete the rule");
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop/rules/{ruleId}");
        }
    }

    private async Task<Guid> SeedAccountAsync(IPage page)
    {
        var username = $"pg-{Guid.NewGuid():N}";
        (await page.APIRequest.PostAsync($"{app.BaseUrl}/api/ctids/",
            new APIRequestContextOptions { DataObject = new { Username = username, Password = "cid_pw_123" } }))
            .Status.Should().Be(200);
        var cids = await GetJsonAsync(page, "/api/ctids/");
        var cidId = cids.EnumerateArray().First(c => c.GetProperty("username").GetString() == username)
            .GetProperty("id").GetGuid();
        (await page.APIRequest.PostAsync($"{app.BaseUrl}/api/ctids/{cidId}/accounts",
            new APIRequestContextOptions { DataObject = new { AccountNumber = 8007777L, Broker = "Pepperstone", IsLive = false, Label = "demo" } }))
            .Status.Should().Be(200);
        var accounts = await GetJsonAsync(page, $"/api/ctids/{cidId}/accounts");
        return accounts.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private async Task<Guid> SeedRuleAsync(IPage page, Guid accountId)
    {
        var create = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop/rules",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    TradingAccountId = accountId,
                    Name = "E2E guard rule",
                    MaxConcurrentLiveInstances = 3,
                    DailyLossLimit = 0.0,
                    MaxDrawdownPercent = 0.0,
                    AutoFlatten = false,
                    Enabled = true
                }
            });
        create.Status.Should().Be(200);
        return JsonDocument.Parse(await create.TextAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetJsonAsync(IPage page, string path)
    {
        var res = await page.APIRequest.GetAsync($"{app.BaseUrl}{path}");
        res.Status.Should().Be(200);
        return JsonDocument.Parse(await res.TextAsync()).RootElement;
    }
}
