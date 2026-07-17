using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class JournalTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Journal_renders_for_a_new_account()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/journal");
        await page.WaitForAppReadyAsync();

        // A fresh account has no runs/backtests → the coaching empty state renders (proves the wired path).
        await Assertions.Expect(page.Locator("[data-testid=journal-empty], [data-testid=journal-summary]").First)
            .ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Manual_note_can_be_added_edited_and_deleted()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/journal");
        await page.WaitForAppReadyAsync();

        await Assertions.Expect(page.Locator("[data-testid=journal-notes-empty]")).ToBeVisibleAsync(Slow);

        // Add via the dialog (mandate 7).
        await page.ClickUntilVisibleAsync("[data-testid=journal-new-entry]", page.Locator(".mud-dialog"));
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);
        await page.GetByTestId("journal-note-title").FillAsync("Held a loser too long");
        await page.GetByTestId("journal-note-body").FillAsync("Should have cut at the stop.");
        await page.ClickAsync("[data-testid=journal-note-save]");

        await Assertions.Expect(page.Locator("[data-testid=journal-notes]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=journal-notes]")).ToContainTextAsync("Held a loser too long");

        // Edit.
        await page.ClickUntilVisibleAsync("[data-testid=journal-note-edit]", page.Locator(".mud-dialog"));
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);
        await page.GetByTestId("journal-note-title").FillAsync("Held a loser — fixed the rule");
        await page.ClickAsync("[data-testid=journal-note-save]");
        await Assertions.Expect(page.Locator("[data-testid=journal-notes]")).ToContainTextAsync("fixed the rule");

        // Delete (confirmation dialog).
        await page.ClickUntilVisibleAsync("[data-testid=journal-note-delete]", page.Locator(".mud-dialog"));
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);
        await page.ClickAsync(".mud-dialog button:has-text('Delete')");
        await Assertions.Expect(page.Locator("[data-testid=journal-notes-empty]")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task New_note_dialog_requires_a_title()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/journal");
        await page.WaitForAppReadyAsync();

        await page.ClickUntilVisibleAsync("[data-testid=journal-new-entry]", page.Locator(".mud-dialog"));
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);
        // Submit with an empty title → dialog stays open (validation blocks close).
        await page.ClickAsync("[data-testid=journal-note-save]");
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);
    }
}
