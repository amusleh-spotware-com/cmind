using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests;

// Stronger than PageSmokeTests: also fails when the app's ErrorBoundary catches a component exception
// (the "Something went wrong on this page." UI), which does NOT trip .blazor-error-ui and so slipped past
// the old smoke test. Dumps the app log tail on failure so the throwing route is diagnosable.
[Collection(AppCollection.Name)]
public sealed class PageHealthTests(AppFixture app, ITestOutputHelper output)
{
    public static IEnumerable<object[]> Routes() => new[]
    {
        "/", "/accounts", "/cbots", "/run", "/backtest", "/nodes", "/copy-trading", "/agent", "/alerts",
        "/prop-firm", "/prop-guard", "/mcp", "/users", "/account", "/optimize",
        "/ai/build", "/ai/review", "/ai/debate", "/ai/sentiment", "/ai/exposure", "/ai/digest",
        "/ai/tune", "/ai/optimize",
        "/settings/ai", "/settings/openapi", "/settings/features", "/settings/legal",
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Page_does_not_trip_error_boundary(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        // Give any OnAfterRender interactive work a beat to throw.
        await page.WaitForTimeoutAsync(500);

        var blazorError = await page.Locator(".blazor-error-ui").IsVisibleAsync();
        var boundaryError = await page.Locator("[data-testid=page-error]").IsVisibleAsync();

        if (blazorError || boundaryError)
        {
            output.WriteLine($"Route {route} errored. App log tail:");
            output.WriteLine(Tail(app.AppLog, 60));
        }

        blazorError.Should().BeFalse($"{route} tripped the Blazor circuit error UI");
        boundaryError.Should().BeFalse($"{route} tripped the ErrorBoundary (component threw)");
    }

    private static string Tail(string s, int lines)
    {
        var all = s.Split('\n');
        return string.Join('\n', all.Skip(Math.Max(0, all.Length - lines)));
    }
}
