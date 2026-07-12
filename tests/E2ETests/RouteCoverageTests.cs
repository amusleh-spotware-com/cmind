using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace E2ETests;

// WS-1 launch gate (public-launch-readiness.md): every routable Blazor page is smoke-tested.
// PageSmokeTests.Routes() is a hand-maintained list; without this gate a new @page can ship with
// zero E2E coverage and no reviewer would notice. This source-scans src/Web for @page directives and
// asserts each concrete (non-parameterized, non-auth) route is present in the smoke list — so adding a
// page forces adding its smoke coverage in the same change, or the build fails.
public sealed partial class RouteCoverageTests
{
    // Routes intentionally outside PageSmokeTests: parameterized routes (no value to navigate to
    // without fixture data — covered by their own feature E2E) and the pre-auth / self-serve pages
    // (PageSmokeTests navigates as the signed-in owner, so these are exercised elsewhere).
    private static readonly HashSet<string> ExcludedRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/login", "/login/2fa", "/register",
    };

    [Fact]
    public void Every_blazor_page_route_is_covered_by_PageSmokeTests()
    {
        var webRoot = Path.Combine(FindRepoRoot(), "src", "Web");
        Directory.Exists(webRoot).Should().BeTrue("src/Web must exist to scan for @page routes");

        var smoked = PageSmokeTests.Routes()
            .Select(row => (string)row[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = DiscoverPageRoutes(webRoot)
            .Where(route => !route.Contains('{', StringComparison.Ordinal))
            .Where(route => !ExcludedRoutes.Contains(route))
            .Where(route => !smoked.Contains(route))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        uncovered.Should().BeEmpty(
            "every Blazor page must be in PageSmokeTests.Routes() (WS-1). Add these routes there (or, if "
            + "parameterized/pre-auth, to RouteCoverageTests.ExcludedRoutes with a reason):\n  {0}",
            string.Join("\n  ", uncovered));
    }

    private static IEnumerable<string> DiscoverPageRoutes(string webRoot)
    {
        var pattern = PageDirective();
        foreach (var file in Directory.EnumerateFiles(webRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var line in File.ReadLines(file))
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    yield return match.Groups["route"].Value;
                }
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "cmind.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (cmind.slnx) from test output.");
    }

    [GeneratedRegex("""^\s*@page\s+"(?<route>[^"]+)"""")]
    private static partial Regex PageDirective();
}
