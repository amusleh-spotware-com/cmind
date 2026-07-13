using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Regression for B-01 (raw GUID in heading) and B-02 (blank page / no notice when the instance is
// missing or terminal). A detail page for any entity state must render an actionable notice, never a
// blank shell, and must not leak the strong-ID Guid as the user-facing title.
[Collection(AppCollection.Name)]
public sealed class InstanceDetailTests(AppFixture app)
{
    [Fact]
    public async Task Missing_instance_shows_not_found_notice_and_does_not_crash()
    {
        var page = await app.NewAuthedPageAsync();
        var missingId = "00000000-0000-0000-0000-0000000000ff";
        await page.GotoAsync($"/instance/{missingId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator("[data-testid=instance-not-found]").IsVisibleAsync())
            .Should().BeTrue("a missing instance must show an actionable not-found notice");

        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse("a missing instance must not trip the Blazor error UI");
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync())
            .Should().BeFalse("a missing instance must not trip the ErrorBoundary");
    }

    [Fact]
    public async Task Missing_instance_heading_does_not_leak_the_raw_guid()
    {
        var page = await app.NewAuthedPageAsync();
        var missingId = "12345678-90ab-cdef-1234-567890abcdef";
        await page.GotoAsync($"/instance/{missingId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var heading = await page.Locator("h5").First.InnerTextAsync();
        heading.Should().NotContain(missingId, "the detail heading must never render the strong-ID Guid");
    }
}
