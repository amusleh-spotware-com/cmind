using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;

namespace E2ETests;

// Shared helpers for Agent Studio E2E: an agent now requires at least one managed account at creation
// (mandate 11 — no doomed action), so every create-through-the-dialog test must seed a real trading
// account and select it before submitting. Seeding goes through the same public ctid/account endpoints a
// user would use.
internal static class AgentTestHelpers
{
    public static async Task<(long Number, Guid CtidId)> SeedTradingAccountAsync(IPage page, string baseUrl)
    {
        var username = $"ag-{Guid.NewGuid():N}";
        (await page.APIRequest.PostAsync($"{baseUrl}/api/ctids/",
                new APIRequestContextOptions { DataObject = new { Username = username, Password = "cid_pw_123" } }))
            .Status.Should().Be(200);

        var cidsResponse = await page.APIRequest.GetAsync($"{baseUrl}/api/ctids/");
        using var cids = JsonDocument.Parse(await cidsResponse.TextAsync());
        var cidId = cids.RootElement.EnumerateArray()
            .First(c => c.GetProperty("username").GetString() == username)
            .GetProperty("id").GetGuid();

        var accountNumber = Random.Shared.NextInt64(1_000_000, 9_999_999);
        (await page.APIRequest.PostAsync($"{baseUrl}/api/ctids/{cidId}/accounts",
                new APIRequestContextOptions
                {
                    DataObject = new { AccountNumber = accountNumber, Broker = "Pepperstone", IsLive = false, Label = "demo" }
                }))
            .Status.Should().Be(200);

        return (accountNumber, cidId);
    }

    // Removes a seeded cTrader ID (cascades to its accounts) so a shared/first-run fixture returns to its
    // prior zero-account state — the sibling "no trading account" gating tests depend on it. Best-effort.
    public static async Task DeleteSeededCtidAsync(IPage page, string baseUrl, Guid ctidId) =>
        await page.APIRequest.DeleteAsync($"{baseUrl}/api/ctids/{ctidId}");

    // Opens the managed-accounts MudSelect in the create/edit dialog, ticks the given account by its
    // number, then closes the multi-select popover so the dialog's action buttons are clickable again.
    public static async Task SelectManagedAccountAsync(IPage page, long accountNumber)
    {
        await page.ClickAsync("[data-testid=agent-accounts]");
        await page.ClickAsync($".mud-list-item:has-text(\"{accountNumber}\")");
        // Close the multi-select popover by clicking its full-viewport backdrop overlay at the top-left
        // corner — clear of the popover itself (a centred click lands on the popover on mobile). Re-clicking
        // the activator is intercepted by the overlay, and an Escape press would bubble up and close the
        // whole dialog; this leaves the dialog and its action buttons intact and clickable.
        await page.ClickAsync(".mud-popover-provider .mud-overlay", new PageClickOptions { Position = new Position { X = 3, Y = 3 } });
    }
}
