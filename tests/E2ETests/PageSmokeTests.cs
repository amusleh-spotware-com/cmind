using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Navigates to every static Blazor page as the signed-in owner and asserts it renders without a crash:
// the initial GET is not a 5xx (a prerender exception surfaces there) and the Blazor unhandled-error UI is
// not shown (an interactive-render exception surfaces there). Catches "navigating to page X crashes".
[Collection(AppCollection.Name)]
public sealed class PageSmokeTests(AppFixture app)
{
    public static IEnumerable<object[]> Routes() => new[]
    {
        "/", "/accounts", "/cbots", "/run", "/backtest", "/nodes", "/copy-trading", "/agent", "/alerts",
        "/prop-firm", "/prop-guard", "/mcp", "/users", "/account", "/optimize", "/quant/integrity", "/quant/sizing", "/quant/health", "/quant/regimes", "/quant/tca", "/quant/positioning", "/quant/execution",
        "/ai/build", "/ai/review", "/ai/debate", "/ai/sentiment", "/ai/exposure", "/ai/digest",
        "/ai/tune", "/ai/optimize", "/agent-studio", "/journal", "/economic-calendar",
        "/settings/ai", "/settings/openapi", "/settings/features", "/settings/legal",
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Page_loads_without_crashing(string route)
    {
        var page = await app.NewAuthedPageAsync();
        var response = await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        response.Should().NotBeNull();
        response!.Status.Should().BeLessThan(500, $"GET {route} returned {response.Status} (prerender crash)");

        // Blazor Server shows this element only when an unhandled exception breaks the circuit.
        var errorUi = page.Locator(".blazor-error-ui");
        (await errorUi.IsVisibleAsync()).Should().BeFalse($"{route} tripped the Blazor error UI (interactive crash)");

        // The app's ErrorBoundary catches component exceptions without tripping .blazor-error-ui, so
        // assert it too — otherwise a thrown page (e.g. a gated-off API 404) slips past this smoke test.
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse($"{route} tripped the ErrorBoundary (component threw)");

        // The app shell rendered (the MudBlazor layout app bar) — the page did not blank out.
        (await page.Locator(".mud-appbar, header, nav").CountAsync()).Should().BeGreaterThan(0, $"{route} did not render the shell");
    }
}
