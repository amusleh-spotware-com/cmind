using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Verifies the app is installable as a PWA: a valid branded manifest with real icons is served, the
// iOS home-screen icon is linked, and the app-shell service worker registers and activates.
[Collection(AppCollection.Name)]
public sealed class PwaTests(AppFixture app)
{
    [Fact]
    public async Task Manifest_is_served_with_icons_and_branding()
    {
        var page = await app.NewAuthedPageAsync();
        var response = await page.GotoAsync("/manifest.webmanifest");

        response.Should().NotBeNull();
        response!.Status.Should().Be(200);
        response.Headers.Should().ContainKey("content-type");
        response.Headers["content-type"].Should().Contain("application/manifest+json");

        var json = await response.JsonAsync();
        json.Should().NotBeNull();
        json!.Value.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        json.Value.GetProperty("display").GetString().Should().Be("standalone");
        json.Value.GetProperty("icons").GetArrayLength().Should().BeGreaterThan(0, "the manifest must ship icons to be installable");

        var hasMaskable = json.Value.GetProperty("icons").EnumerateArray()
            .Any(i => i.GetProperty("purpose").GetString() == "maskable");
        hasMaskable.Should().BeTrue("a maskable icon is needed for a clean Android home-screen icon");
    }

    [Fact]
    public async Task Apple_touch_icon_is_linked()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator("link[rel='apple-touch-icon']").CountAsync())
            .Should().BeGreaterThan(0, "iOS uses apple-touch-icon for the home-screen icon");
    }

    [Fact]
    public async Task Service_worker_registers_and_activates()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var ready = await page.EvaluateAsync<bool>(@"async () => {
            if (!('serviceWorker' in navigator)) return false;
            return await Promise.race([
                navigator.serviceWorker.ready.then(() => true),
                new Promise((r) => setTimeout(() => r(false), 10000))
            ]);
        }");

        ready.Should().BeTrue("the app-shell service worker should register and activate (installability)");
    }
}
