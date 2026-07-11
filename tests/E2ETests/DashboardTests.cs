using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the revamped live dashboard as the signed-in owner: KPI cards, activity chart, status ring,
// live feed, the period toggle (interaction + reload) and drill-down navigation — on desktop and on a
// real mobile viewport. Structural assertions stay resilient to an empty seed (zeros/empty states).
[Collection(AppCollection.Name)]
public sealed class DashboardTests(AppFixture app)
{
    private static async Task WaitLoadedAsync(IPage page)
    {
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        // The dashboard prerenders a skeleton, then the interactive circuit fetches the overview.
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Dashboard_shows_live_kpis_chart_status_and_feed()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        (await page.Locator("[data-testid=live-indicator]").IsVisibleAsync()).Should().BeTrue();

        foreach (var kpi in new[] { "kpi-active", "kpi-success", "kpi-completed", "kpi-failed" })
        {
            (await page.Locator($"[data-testid={kpi}]").IsVisibleAsync())
                .Should().BeTrue($"{kpi} card should render");
        }

        (await page.Locator("[data-testid=status-ring]").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("[data-testid=period-toggle]").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("[data-testid=dash-updated]").IsVisibleAsync()).Should().BeTrue();
        // Feed renders either rows or its empty state.
        (await page.Locator(".app-feed, .app-feed-empty").CountAsync()).Should().BeGreaterThan(0);

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Period_toggle_changes_the_active_period()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        // 24h is the default active period.
        (await page.Locator("[data-testid=period-24h]").GetAttributeAsync("class"))
            .Should().Contain("app-period-active");

        await page.Locator("[data-testid=period-7d]").ClickAsync();
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });

        (await page.Locator("[data-testid=period-7d]").GetAttributeAsync("class"))
            .Should().Contain("app-period-active", "the clicked period becomes active");
        (await page.Locator("[data-testid=period-24h]").GetAttributeAsync("class"))
            .Should().NotContain("app-period-active", "the previous period is deselected");

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    // Regression: switching period must NOT crash the page or drop the circuit. Previously the dashboard
    // nulled its data on switch, which remounted the ApexChart and raced its JS interop into an
    // ErrorBoundary + reconnect. Rapidly cycle every period and assert the chart survives each time.
    [Fact]
    public async Task Cycling_every_period_never_crashes_the_page_or_drops_the_circuit()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        foreach (var period in new[] { "period-1h", "period-7d", "period-30d", "period-24h", "period-1h" })
        {
            await page.Locator($"[data-testid={period}]").ClickAsync();
            await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });

            (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
                .Should().BeFalse($"clicking {period} must not trip the ErrorBoundary");
            (await page.Locator(".blazor-error-ui").IsVisibleAsync())
                .Should().BeFalse($"clicking {period} must not break the circuit");
            (await page.Locator("#components-reconnect-modal").IsVisibleAsync())
                .Should().BeFalse($"clicking {period} must not drop the connection");
        }
    }

    [Fact]
    public async Task Powered_by_link_is_shown_by_default_and_points_to_the_site()
    {
        // Default branding (App:Branding:ShowSiteLink defaults to true) shows the credit link; a
        // white-label deployment hides it by setting ShowSiteLink=false.
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        var link = page.Locator("[data-testid=powered-by-link]");
        (await link.IsVisibleAsync()).Should().BeTrue("the 'Powered by cMind' link shows by default");
        (await link.GetAttributeAsync("href")).Should().Contain("amusleh-spotware-com.github.io/cmind");
    }

    [Fact]
    public async Task Kpi_card_drills_through_to_its_list_page()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        await page.Locator("[data-testid=kpi-active]").ClickAsync();
        await page.WaitForURLAsync("**/run", new() { Timeout = 15000 });

        page.Url.Should().EndWith("/run");
    }

    [Fact]
    public async Task Dashboard_renders_on_mobile_without_horizontal_overflow()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });

        (await page.Locator("[data-testid=kpi-active]").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("[data-testid=status-ring]").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("[data-testid=bottom-nav]").IsVisibleAsync()).Should().BeTrue();

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue("the dashboard must not scroll sideways on a phone");
    }
}
