using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Runs axe-core against key screens and fails on critical/serious accessibility violations. Colour-contrast
// is theme-dependent (white-label sets the palette) so it is checked separately, not here — see
// website/docs/ui-guidelines.md §8.
[Collection(AppCollection.Name)]
public sealed class AccessibilityTests(AppFixture app)
{
    private static readonly string AxePath = Path.Combine(AppContext.BaseDirectory, "axe.min.js");

    // color-contrast is theme-dependent (white-label) and checked separately. aria-dialog-name is a
    // MudBlazor gap: MudMessageBox (the AI-not-configured popup) doesn't set an accessible name on its
    // dialog element — framework chrome, not our markup.
    private const string RunAxe = @"async () => {
        const r = await axe.run(document, {
            resultTypes: ['violations'],
            rules: { 'color-contrast': { enabled: false }, 'aria-dialog-name': { enabled: false } }
        });
        return r.violations
            .filter(v => v.impact === 'critical' || v.impact === 'serious')
            .map(v => v.id + ' (' + v.impact + ', ' + v.nodes.length + ')');
    }";

    public static IEnumerable<object[]> AuthedRoutes() => new[]
    {
        "/", "/cbots", "/nodes", "/users", "/account",
        "/mcp", "/prop-firm", "/copy-trading", "/accounts", "/alerts", "/agent",
        "/settings/ai", "/settings/openapi", "/settings/features", "/settings/legal",
        "/run", "/backtest", "/prop-guard", "/optimize",
        "/ai/build", "/ai/review", "/ai/optimize",
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AuthedRoutes))]
    public async Task Page_has_no_critical_or_serious_a11y_violations(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.AddScriptTagAsync(new() { Path = AxePath });

        var violations = await page.EvaluateAsync<string[]>(RunAxe);
        violations.Should().BeEmpty($"{route} must have no critical/serious a11y violations, found: {string.Join(", ", violations)}");
    }

    [Fact]
    public async Task Login_has_no_critical_or_serious_a11y_violations()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.AddScriptTagAsync(new() { Path = AxePath });

        var violations = await page.EvaluateAsync<string[]>(RunAxe);
        violations.Should().BeEmpty($"login must have no critical/serious a11y violations, found: {string.Join(", ", violations)}");
    }
}
