using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The working-path counterpart to EconomicCalendarE2ETests (which asserts the source-LESS gate). With a
// real FRED/BLS key configured and ingestion on, this drives the calendar through the real UI and the JWT
// cBot API against LIVE provider data — mandate 11's "working path when the dependency is present". Every
// test no-ops (skips) when no key is configured, via the fixture declining to boot.
[Collection(CalendarLiveCollection.Name)]
public sealed class EconomicCalendarLiveE2ETests(CalendarLiveFixture app)
{
    private static readonly string[] ReadScope = ["calendar:read"];

    [Fact]
    public async Task Nav_shows_the_calendar_link_when_a_source_is_configured()
    {
        if (!app.HasSource) return;

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // CalendarEnablement.IsVisible ⇒ enabled AND a source key present ⇒ the nav entry appears.
        await page.Locator("a[href='/economic-calendar']").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
    }

    [Fact]
    public async Task Calendar_page_shows_the_working_path_not_the_source_notice()
    {
        if (!app.HasSource) return;

        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/economic-calendar", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // Source configured ⇒ the source-required notice is gone and the Filters action is available.
        (await page.Locator("[data-testid=calendar-source-required]").CountAsync())
            .Should().Be(0, "a configured source clears the source-required notice");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filters" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
    }

    [Fact]
    public async Task Filter_dialog_opens_and_applies_without_error()
    {
        if (!app.HasSource) return;

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/economic-calendar", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filters" }).ClickAsync();
        await page.Locator(".mud-dialog").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Apply the (unchanged) filter — the page must reload its window and close the dialog without error.
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Apply" }).ClickAsync();
        await page.Locator(".mud-dialog").First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Series_page_renders_the_working_path_for_a_seeded_series()
    {
        if (!app.HasSource) return;

        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/economic-calendar/series/US.CPI",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=calendar-source-required]").CountAsync())
            .Should().Be(0, "the series page has a source configured");
        (await page.GetByText("US.CPI").First.IsVisibleAsync()).Should().BeTrue();
    }

    // Full pipeline: live FRED/BLS fetch → append-only write → read model → JWT cBot API. Mints a real API
    // client + token, then polls /history over a wide window until the ingestion worker has landed real
    // releases — proving the keys actually pull data end-to-end, not merely that the UI un-gates.
    [Fact]
    public async Task Jwt_api_serves_live_ingested_events()
    {
        if (!app.HasSource) return;

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var api = page.APIRequest;

        var createResponse = await api.PostAsync("/api/calendar/clients", new APIRequestContextOptions
        {
            DataObject = new { Name = "e2e-live", Scopes = ReadScope, ExpiresInDays = 1 }
        });
        createResponse.Ok.Should().BeTrue("the owner can mint a calendar API client");
        var created = (await createResponse.JsonAsync())!.Value;
        var clientId = created.GetProperty("clientId").GetString();
        var clientSecret = created.GetProperty("clientSecret").GetString();

        var tokenResponse = await api.PostAsync("/api/calendar/v1/token", new APIRequestContextOptions
        {
            DataObject = new { ClientId = clientId, ClientSecret = clientSecret }
        });
        tokenResponse.Ok.Should().BeTrue("valid client credentials mint a JWT");
        var token = (await tokenResponse.JsonAsync())!.Value.GetProperty("token").GetString();

        var authHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
        // Wide historical window (FRED publishes realized observations, not a forward schedule); fixed
        // literal dates — no wall clock in tests (mandate 4).
        const string historyPath = "/api/calendar/v1/history?from=2015-01-01&to=2035-01-01&limit=50";

        var found = false;
        for (var attempt = 0; attempt < 30 && !found; attempt++)
        {
            var historyResponse = await api.GetAsync(
                historyPath, new APIRequestContextOptions { Headers = authHeaders });
            historyResponse.Ok.Should().BeTrue("the scoped JWT authorizes the history read");
            var events = (await historyResponse.JsonAsync())!.Value;
            if (events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
                found = true;
            else
                await Task.Delay(TimeSpan.FromSeconds(3));
        }

        found.Should().BeTrue(
            "live FRED/BLS ingestion must land at least one event that the JWT history API returns within the polling window");
    }
}
