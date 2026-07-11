using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests;

// Regression for the real production failure: with the Authoring feature disabled, /api/cbots returns 404.
// The Agent and Assistant pages load /api/cbots on init, so an unguarded call threw into the ErrorBoundary
// ("Something went wrong on this page.") — which the empty-DB smoke tests never hit because they run with
// every feature enabled. This disables Authoring, asserts the pages still render, and restores it.
[Collection(AppCollection.Name)]
public sealed class AiPagesWithDataTests(AppFixture app, ITestOutputHelper output)
{
    [Theory]
    [InlineData("/agent")]
    [InlineData("/assistant")]
    public async Task Page_renders_when_authoring_feature_disabled(string route)
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var disable = await api.PutAsync("/api/features/Authoring", new() { DataObject = new { Enabled = false } });
        disable.Ok.Should().BeTrue($"disable Authoring failed: {disable.Status} {await disable.TextAsync()}");
        try
        {
            await WaitForCbotsStatusAsync(api, 404);

            await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForTimeoutAsync(500);

            var boundaryError = await page.Locator("[data-testid=page-error]").IsVisibleAsync();
            var blazorError = await page.Locator(".blazor-error-ui").IsVisibleAsync();
            if (boundaryError || blazorError)
            {
                output.WriteLine($"{route} errored with Authoring disabled. App log tail:");
                output.WriteLine(string.Join('\n', app.AppLog.Split('\n')[^100..]));
            }

            boundaryError.Should().BeFalse($"{route} tripped the ErrorBoundary when Authoring is disabled (/api/cbots 404)");
            blazorError.Should().BeFalse($"{route} tripped the Blazor error UI when Authoring is disabled");
        }
        finally
        {
            await api.PutAsync("/api/features/Authoring", new() { DataObject = new { Enabled = true } });
            await WaitForCbotsStatusAsync(api, 200);
        }
    }

    // Feature overrides are cached (~10s TTL), so poll until the gate reflects the toggle before asserting.
    private static async Task WaitForCbotsStatusAsync(IAPIRequestContext api, int expected)
    {
        for (var i = 0; i < 40; i++)
        {
            var r = await api.GetAsync("/api/cbots/");
            if (r.Status == expected) return;
            await Task.Delay(500);
        }
        throw new TimeoutException($"/api/cbots/ did not reach status {expected} within timeout.");
    }
}
