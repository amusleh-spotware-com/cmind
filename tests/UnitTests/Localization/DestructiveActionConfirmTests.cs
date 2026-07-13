using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

// R3 gate (A-06/B-03/D-05/D-06: delete/erase fired on a single click with no confirmation — found only by
// manual clicking). A component that performs a destructive HTTP call (DELETE, or an /erase POST) MUST
// route it through the shared Dialogs.ConfirmAsync(...) so the user confirms first. Census + ratchet: scan
// every component, and any destructive component NOT calling ConfirmAsync must be in the shrinking
// destructive-confirm-baseline.txt. A new unconfirmed destructive action fails the build; the baseline
// may only shrink. When empty, delete it and every destructive action is confirmed.
public sealed class DestructiveActionConfirmTests
{
    private static readonly string[] DestructiveMarkers =
        ["Http.DeleteAsync(", "\"/api/compliance/erase\"", "/erase\""];

    [Fact]
    public void Every_destructive_component_confirms_or_is_in_the_shrinking_baseline()
    {
        var root = RepoPaths.WebComponents;
        var offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            .Where(x => DestructiveMarkers.Any(m => x.Text.Contains(m, StringComparison.Ordinal)))
            // A confirmation is either the shared ConfirmAsync helper or a MudBlazor ShowMessageBox
            // (both present the user a cancelable confirm before the destructive call).
            .Where(x => !x.Text.Contains("ConfirmAsync", StringComparison.Ordinal)
                     && !x.Text.Contains("ShowMessageBox", StringComparison.Ordinal))
            .Select(x => Path.GetRelativePath(root, x.Path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var baseline = LoadBaseline();

        var newlyUnconfirmed = offenders.Except(baseline, StringComparer.OrdinalIgnoreCase).Order().ToArray();
        newlyUnconfirmed.Should().BeEmpty(
            "these components perform a destructive action without Dialogs.ConfirmAsync — gate it behind a "
            + "confirmation dialog (mandate 7/11):\n  {0}",
            string.Join("\n  ", newlyUnconfirmed));

        var stale = baseline.Except(offenders, StringComparer.OrdinalIgnoreCase).Order().ToArray();
        stale.Should().BeEmpty(
            "these components now confirm — remove them from destructive-confirm-baseline.txt so the ratchet "
            + "keeps shrinking:\n  {0}",
            string.Join("\n  ", stale));
    }

    private static HashSet<string> LoadBaseline()
    {
        var path = Path.Combine(RepoPaths.LocalizationTestDir, "destructive-confirm-baseline.txt");
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
