using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Covers the split-out AI feature pages (one per former Assistant tab) and the "AI not configured" notice
// that now appears on every AI page including Portfolio Agent, Alerts, and MCP Keys. The test app has no
// AI key, so the notice banner must show and no page may trip the ErrorBoundary.
[Collection(AppCollection.Name)]
public sealed class AiPagesTests(AppFixture app)
{
    public static IEnumerable<object[]> AiFeatureRoutes() => new[]
    {
        "/ai/build", "/ai/review", "/ai/debate", "/ai/sentiment",
        "/ai/exposure", "/ai/digest", "/ai/tune", "/ai/optimize",
    }.Select(r => new object[] { r });

    public static IEnumerable<object[]> NoticeRoutes() => new[]
    {
        "/ai/build", "/agent", "/alerts", "/mcp",
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AiFeatureRoutes))]
    public async Task Ai_feature_page_renders_without_error(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse($"{route} tripped the ErrorBoundary");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse($"{route} tripped the Blazor error UI");
        (await page.Locator("h5, .mud-typography-h5").First.IsVisibleAsync())
            .Should().BeTrue($"{route} did not render its heading");
    }

    [Theory]
    [MemberData(nameof(NoticeRoutes))]
    public async Task Ai_page_shows_not_configured_notice(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.Locator("[data-testid=ai-not-configured]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });
    }

    [Fact]
    public async Task Old_assistant_route_is_gone()
    {
        var page = await app.NewAuthedPageAsync();
        var response = await page.GotoAsync("/assistant");
        response!.Status.Should().Be(404, "the nested AI/Assistant page was replaced by per-feature pages");
    }
}
