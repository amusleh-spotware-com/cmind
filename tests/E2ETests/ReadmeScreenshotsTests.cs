using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Generates the README screenshot gallery from a live app boot. Skipped in normal runs; set
// CAPTURE_SCREENSHOTS=1 to write PNGs into design/screenshots/.
[Collection(AppCollection.Name)]
public sealed class ReadmeScreenshotsTests(AppFixture app)
{
    private static readonly string OutDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../design/screenshots"));

    [Fact]
    public async Task Capture_readme_gallery()
    {
        if (Environment.GetEnvironmentVariable("CAPTURE_SCREENSHOTS") != "1") return;

        Directory.CreateDirectory(OutDir);

        // Login (desktop) — anonymous.
        var login = await app.NewAnonymousPageAsync();
        await login.SetViewportSizeAsync(1440, 900);
        await login.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await login.WaitForTimeoutAsync(700);
        await Shot(login, "login-desktop.png");

        // Login (mobile).
        var loginMobile = await app.NewAnonymousMobilePageAsync();
        await loginMobile.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await loginMobile.WaitForTimeoutAsync(700);
        await Shot(loginMobile, "login-mobile.png");

        // Authed desktop pages.
        foreach (var (route, file) in new[]
                 {
                     ("/", "dashboard-desktop.png"),
                     ("/cbots", "cbots-desktop.png"),
                     ("/ai/build", "ai-build-desktop.png"),
                     ("/nodes", "nodes-desktop.png"),
                     ("/copy-trading", "copy-trading-desktop.png"),
                 })
        {
            var page = await app.NewAuthedPageAsync();
            await page.SetViewportSizeAsync(1440, 900);
            await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForTimeoutAsync(700);
            await Shot(page, file);
        }

        // Dashboard on mobile — the mobile-first story.
        var dashMobile = await app.NewAuthedMobilePageAsync();
        await dashMobile.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await dashMobile.WaitForTimeoutAsync(700);
        await Shot(dashMobile, "dashboard-mobile.png");
    }

    private static Task Shot(IPage page, string file) =>
        page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(OutDir, file), FullPage = false });
}
