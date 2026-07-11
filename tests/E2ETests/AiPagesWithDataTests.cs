using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests;

// Reproduces the data-dependent failures the empty-DB smoke tests miss: the Agent and Assistant pages
// render trading accounts (and cBots) in selects, so they only exercise the dynamic-id rendering path when
// data exists. Seeds an account, then asserts the pages do not trip the ErrorBoundary.
[Collection(AppCollection.Name)]
public sealed class AiPagesWithDataTests(AppFixture app, ITestOutputHelper output)
{
    [Theory]
    [InlineData("/agent")]
    [InlineData("/assistant")]
    public async Task Page_renders_with_a_trading_account(string route)
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var ctidResp = await api.PostAsync("/api/ctids", new() { DataObject = new { Username = $"repro-{Guid.NewGuid():N}@e2e.local", Password = "Repro_Pass_123!" } });
        ctidResp.Ok.Should().BeTrue($"create cid failed: {ctidResp.Status} {await ctidResp.TextAsync()}");
        var ctidId = (await ctidResp.JsonAsync())!.Value.GetProperty("id").GetString();

        var acctResp = await api.PostAsync($"/api/ctids/{ctidId}/accounts",
            new() { DataObject = new { AccountNumber = 1234567L, Broker = "ReproBroker", IsLive = false, Label = (string?)null } });
        acctResp.Ok.Should().BeTrue($"create account failed: {acctResp.Status} {await acctResp.TextAsync()}");

        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(500);

        var boundaryError = await page.Locator("[data-testid=page-error]").IsVisibleAsync();
        var blazorError = await page.Locator(".blazor-error-ui").IsVisibleAsync();
        if (boundaryError || blazorError)
        {
            output.WriteLine($"{route} errored with data. App log tail:");
            output.WriteLine(string.Join('\n', app.AppLog.Split('\n')[^80..]));
        }

        boundaryError.Should().BeFalse($"{route} tripped the ErrorBoundary when a trading account exists");
        blazorError.Should().BeFalse($"{route} tripped the Blazor error UI when a trading account exists");
    }
}
