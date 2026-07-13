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

    // I-02: the manifest <link> href must be an absolute path (leading '/'), not a relative one, so the
    // install prompt keeps working under a sub-path deployment; and the manifest itself must resolve (200).
    [Fact]
    public async Task Manifest_link_uses_absolute_path_and_resolves()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var href = await page.Locator("link[rel='manifest']").First.GetAttributeAsync("href");
        href.Should().NotBeNullOrWhiteSpace("the PWA manifest must be linked");
        href!.Should().StartWith("/", "a relative manifest href breaks sub-path deployments");

        var response = await page.APIRequest.GetAsync(app.BaseUrl + href);
        response.Status.Should().Be(200, "the linked manifest must be reachable");
    }

    // I-07: the offline fallback page is served (200) with real content, and the app-shell service worker
    // has actually cached it so a navigate-time network failure can fall back to it. Asserting the SW cache
    // holds the offline page (rather than emulating a dropped socket) is the reliable cross-browser way to
    // prove the offline fallback is wired — Playwright's SetOfflineAsync does not reliably reach the
    // service-worker's own fetch in headless Chromium/Edge.
    [Fact]
    public async Task Offline_fallback_page_is_served_and_cached_by_the_service_worker()
    {
        var page = await app.NewAuthedPageAsync();

        var offline = await page.APIRequest.GetAsync(app.BaseUrl + "/offline.html");
        offline.Status.Should().Be(200, "the PWA offline fallback page must be served");
        (await offline.TextAsync()).Should().Contain("offline", "the offline page explains the app is offline");

        // Register + activate the service worker (it precaches /offline.html on install).
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var ready = await page.EvaluateAsync<bool>(@"async () => {
            if (!('serviceWorker' in navigator)) return false;
            return await Promise.race([
                navigator.serviceWorker.ready.then(() => true),
                new Promise((r) => setTimeout(() => r(false), 10000))
            ]);
        }");
        ready.Should().BeTrue("the service worker must be active to serve the offline fallback");

        // The SW must have precached the offline page in a cache so it can be served when the network drops.
        var offlineCached = await page.EvaluateAsync<bool>(@"async () => {
            const keys = await caches.keys();
            for (const k of keys) {
                const c = await caches.open(k);
                const hit = await c.match('/offline.html');
                if (hit) {
                    const text = await hit.text();
                    if (text.toLowerCase().includes('offline')) return true;
                }
            }
            return false;
        }");
        offlineCached.Should().BeTrue("the service worker must cache /offline.html so the offline navigate fallback works");
    }
}
