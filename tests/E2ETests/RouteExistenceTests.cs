using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace E2ETests;

// R7 gate (E-06: AiPagesWithDataTests drove the deleted /assistant route for months, a green test that
// exercised nothing). Scans every E2E source file for route string literals navigated to
// (GotoAsync("/x"), [InlineData("/x")], APIRequest paths are excluded) and asserts each app-page route
// resolves to a real @page in src/Web — so deleting a page forces fixing or deleting its test, and a
// stale test can never masquerade as coverage.
public sealed partial class RouteExistenceTests
{
    // Non-page routes that are endpoints/assets, not @page components — legitimately referenced by E2E
    // but not backed by a Blazor @page. Keep tight; add with a reason.
    private static readonly HashSet<string> NonPageRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/set-culture", "/set-timezone", "/error", "/forbidden", "/logout",
        "/openapi/callback",                 // minimal-API OAuth callback endpoint (OpenApiEndpoints)
        "/assistant",                        // deliberately removed; Old_assistant_route_is_gone asserts it 404s
    };

    [Fact]
    public void Every_route_navigated_in_e2e_resolves_to_a_real_page()
    {
        var repoRoot = FindRepoRoot();
        var pageRoutes = DiscoverPageRoutes(Path.Combine(repoRoot, "src", "Web"));
        var e2eDir = Path.Combine(repoRoot, "tests", "E2ETests");

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(e2eDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.EndsWith("RouteExistenceTests.cs", StringComparison.Ordinal))  // skip our own doc examples
                continue;
            var text = File.ReadAllText(file);
            foreach (Match m in NavRouteRegex().Matches(text))
                referenced.Add(NormalizeRoute(m.Groups["route"].Value));
        }

        var dangling = referenced
            .Where(r => r.Length > 1 && !r.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            .Where(r => !r.Contains('.', StringComparison.Ordinal))   // static assets (favicon.svg, icons/*.png, *.webmanifest)
            .Where(r => !NonPageRoutes.Contains(r))
            .Where(r => !RouteExists(r, pageRoutes))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        dangling.Should().BeEmpty(
            "these routes are navigated in E2E tests but no @page in src/Web serves them — the test is "
            + "stale (fix or delete it), or add the route to a NonPageRoutes exclusion with a reason:\n  {0}",
            string.Join("\n  ", dangling));
    }

    private static bool RouteExists(string route, IReadOnlyCollection<string> pageRoutes)
    {
        foreach (var p in pageRoutes)
        {
            if (string.Equals(p, route, StringComparison.OrdinalIgnoreCase)) return true;
            // A parameterized page (/instance/{id}) matches a concrete nav (/instance/abc).
            var prefix = p.IndexOf('{', StringComparison.Ordinal);
            if (prefix > 0 && route.StartsWith(p[..prefix], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string NormalizeRoute(string route)
    {
        var q = route.IndexOfAny(['?', '#']);
        if (q >= 0) route = route[..q];
        return route.Length > 1 ? route.TrimEnd('/') : route;
    }

    private static IReadOnlyCollection<string> DiscoverPageRoutes(string webRoot)
    {
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(webRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            foreach (var line in File.ReadLines(file))
            {
                var m = PageDirectiveRegex().Match(line);
                if (m.Success) routes.Add(NormalizeRoute(m.Groups["route"].Value));
            }
        }
        return routes;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "cmind.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (cmind.slnx).");
    }

    // GotoAsync("/x") and [InlineData("/x")] — the two ways E2E names a page route to visit.
    [GeneratedRegex(@"(?:GotoAsync\(\s*(?:\$?""(?:\{app\.BaseUrl\})?)|InlineData\(\s*"")(?<route>/[A-Za-z0-9/_\-{}?=&.]*)""")]
    private static partial Regex NavRouteRegex();

    [GeneratedRegex("^\\s*@page\\s+\"(?<route>[^\"]+)\"")]
    private static partial Regex PageDirectiveRegex();
}
