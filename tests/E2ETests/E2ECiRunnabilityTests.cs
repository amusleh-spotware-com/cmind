using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace E2ETests;

// Census gate (CLAUDE.md mandate 12) for E2E tests that must run inside GitHub Actions. The CI E2E job
// (.github/workflows/ci.yml → test-e2e) runs the WHOLE project unfiltered on ubuntu-latest with only the
// Playwright **chromium** browser installed. A new E2E test can silently fail — or never run — in CI if it
// (a) is excluded by a --filter added to the CI command, (b) launches a browser engine CI didn't install
// (Firefox/WebKit), or (c) boots its own browser/app instead of the single CI-provisioned AppFixture. This
// scans the workflow + every E2E source file and fails the build on any of those, so "runs on my machine"
// can never diverge from "runs in GitHub Actions". Opt-out sets are ratchets — they may only shrink.
public sealed partial class E2ECiRunnabilityTests
{
    // The one sanctioned place a browser engine/channel is chosen (guarded, with a Chromium fallback CI has).
    private static readonly HashSet<string> BrowserSelectionAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppFixture.cs",
    };

    // E2E test files that legitimately do NOT drive the app through the shared fixture (pure file-scan gates
    // like this one). They need no browser, so they need no [Collection(AppCollection.Name)].
    private static readonly HashSet<string> NoFixtureAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "E2ECiRunnabilityTests.cs", "RouteExistenceTests.cs",
    };

    [Fact]
    public void Ci_runs_the_whole_e2e_project_unfiltered_with_the_browser_the_harness_uses()
    {
        var ci = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "ci.yml"));

        var e2eCommand = CiE2eCommandRegex().Match(ci);
        e2eCommand.Success.Should().BeTrue("ci.yml must run `dotnet test tests/E2ETests` in the E2E job");
        e2eCommand.Value.Should().NotContain("--filter",
            "the CI E2E command must run EVERY test — a --filter would silently exclude new E2E tests from CI");

        ci.Should().Contain("playwright.ps1 install")
            .And.Contain("chromium",
                "CI must install the chromium browser the AppFixture launches (Edge channel on Windows only, "
                + "plain headless Chromium everywhere else including CI)");
    }

    [Fact]
    public void No_e2e_test_pins_a_browser_engine_ci_does_not_install()
    {
        var offenders = E2eSourceFiles()
            .Where(f => !BrowserSelectionAllowed.Contains(Path.GetFileName(f)) && !IsLiveOnly(f))
            .Where(f => ForeignBrowserRegex().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "these E2E tests select a browser engine/channel CI does not provision (only chromium is installed) "
            + "— drive the app through AppFixture instead:\n  {0}", string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_app_driving_e2e_test_uses_the_shared_ci_provisioned_fixture()
    {
        var offenders = E2eSourceFiles()
            .Where(f => !NoFixtureAllowed.Contains(Path.GetFileName(f)))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                // A class that takes the AppFixture (drives the real app/browser) must sit in the shared
                // collection so it boots via the single fixture CI provisions — not its own browser/app.
                return text.Contains("AppFixture app", StringComparison.Ordinal)
                       && !text.Contains("[Collection(", StringComparison.Ordinal);
            })
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "these E2E tests drive the app but are not in [Collection(AppCollection.Name)] — they would spin up "
            + "an unmanaged browser/app that GitHub Actions cannot provision:\n  {0}",
            string.Join("\n  ", offenders));
    }

    // Live-only tests (tests/E2ETests/CopyLive) run against real broker creds and SKIP cleanly when absent,
    // so they never execute in CI and may launch their own browser (e.g. a real Edge channel). They are the
    // sanctioned exception to the CI-Chromium rule.
    private static bool IsLiveOnly(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}CopyLive{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<string> E2eSourceFiles()
    {
        var dir = Path.Combine(RepoRoot(), "tests", "E2ETests");
        return Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "cmind.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (cmind.slnx).");
    }

    // The E2E test command line in ci.yml (`dotnet test tests/E2ETests ...`), across the `>`-folded YAML lines.
    [GeneratedRegex(@"dotnet test tests/E2ETests[^\r\n]*(?:\r?\n\s+(?!- )[^\r\n]+)*")]
    private static partial Regex CiE2eCommandRegex();

    // Firefox/WebKit engine use, or an explicit browser Channel — none installed in CI outside AppFixture.
    [GeneratedRegex(@"\.\s*(Firefox|Webkit)\b|Channel\s*=")]
    private static partial Regex ForeignBrowserRegex();
}
