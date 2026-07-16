using System.Reflection;
using System.Text.RegularExpressions;
using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests.Architecture;

// Machine-enforced CLAUDE.md mandates. These fail the build the moment a violation is introduced,
// so the rules stop depending on a reviewer noticing. Source-scan guards read the repo tree relative
// to the test assembly (repo root = first ancestor holding cmind.slnx).
public sealed class ArchitectureGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");
    private static readonly string CoreRoot = Path.Combine(SrcRoot, "Core");

    // Opt-out ratchet (mandate #12): raw-Guid identity properties still allowed on Core types, each with a
    // reason. These are append-only fact-log rows (not aggregates) and one polymorphic audit reference — no
    // single strong Id type fits. The set may ONLY shrink: never add an entry to green a build.
    private static readonly HashSet<string> RawGuidIdentityAllowList =
    [
        "CopyExecution.ProfileId",      // append-only transparency fact-log row, not an aggregate
        "CopyNotification.ProfileId",   // append-only notification fact-log row, not an aggregate
        "CopyFeeAccrual.ProfileId",     // append-only fee-accrual fact-log row, not an aggregate
        "CopyFeeAccrual.DestinationId", // fee-accrual destination reference on the same fact-log row
        "AuditLog.EntityId",            // polymorphic audit target (any aggregate type) — no single strong Id fits
    ];

    // Standalone agent processes that ship without the Core source-generated LogMessages catalog.
    private static readonly string[] DirectLoggingExemptProjects = ["CtraderCliNode", "CopyEngine", "CopyAgent"];

    // Mandate #1: src/Core is pure domain — zero infrastructure dependencies.
    [Fact]
    public void Core_assembly_has_no_infrastructure_dependencies()
    {
        string[] forbidden =
        [
            "EntityFrameworkCore", "Npgsql", "Docker", "AspNetCore", "Serilog", "OpenTelemetry",
        ];

        var referenced = typeof(VersionInfo).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .ToArray();

        var leaks = referenced
            .Where(name => forbidden.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        leaks.Should().BeEmpty(
            "src/Core must compile with zero infra deps (CLAUDE.md mandate #1); found: {0}",
            string.Join(", ", leaks));
    }

    // Mandate #4: never DateTime.UtcNow / DateTime.Now / DateTimeOffset.UtcNow — inject TimeProvider.
    [Fact]
    public void No_source_reads_the_ambient_clock()
    {
        var pattern = new Regex(@"\b(DateTime\.UtcNow|DateTime\.Now|DateTimeOffset\.UtcNow)\b");

        var offenders = ScanSource(SrcRoot, pattern, _ => true).ToArray();

        offenders.Should().BeEmpty(
            "domain/app code must read time via injected TimeProvider.GetUtcNow() (mandate #4):\n{0}",
            string.Join("\n", offenders));
    }

    // Mandate #6: log via source-generated LogMessages, never ILogger.LogInformation(...) directly.
    // Scoped to the projects that wire the LogMessages catalog; standalone agents are exempt.
    [Fact]
    public void No_direct_ILogger_calls_outside_standalone_agents()
    {
        var pattern = new Regex(@"\.Log(Information|Warning|Error|Critical|Debug|Trace)\(");

        bool NotExempt(string file) =>
            !DirectLoggingExemptProjects.Any(p =>
                file.Contains(Path.Combine("src", p) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        var offenders = ScanSource(SrcRoot, pattern, NotExempt).ToArray();

        offenders.Should().BeEmpty(
            "use source-generated LogMessages, not ILogger.Log* directly (mandate #6):\n{0}",
            string.Join("\n", offenders));
    }

    // Mandate #1 (strong IDs): an identity-typed property on a Core type uses a strongly-typed Id
    // (IStronglyTypedId<T> — CBotId, InstanceLineageId, …), never a raw Guid. A raw Guid identity is how a
    // run and a backtest silently got mixed (the LineageId lineage bug); a strong Id makes that a compile
    // error. New raw-Guid identity properties are forbidden — extend StrongIds.cs instead.
    [Fact]
    public void Core_identity_properties_are_strongly_typed_not_raw_guid()
    {
        var propPattern = new Regex(@"public\s+Guid\??\s+(\w*Id)\b\s*\{");
        var typePattern = new Regex(@"\b(?:class|record|struct|interface)\s+(\w+)");

        List<string> offenders = [];
        foreach (var file in Directory.EnumerateFiles(CoreRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var currentType = "?";
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var typeMatch = typePattern.Match(line);
                if (typeMatch.Success)
                {
                    currentType = typeMatch.Groups[1].Value;
                }

                var propMatch = propPattern.Match(line);
                if (!propMatch.Success)
                {
                    continue;
                }

                var qualified = $"{currentType}.{propMatch.Groups[1].Value}";
                if (RawGuidIdentityAllowList.Contains(qualified))
                {
                    continue;
                }

                offenders.Add($"  {Path.GetRelativePath(CoreRoot, file)}:{lineNumber} {qualified}");
            }
        }

        offenders.Should().BeEmpty(
            "Core identity properties must be strongly-typed Ids, not raw Guid (mandate #1) — extend "
            + "StrongIds.cs; add to the opt-out ratchet only with a written reason:\n{0}",
            string.Join("\n", offenders));
    }

    // The app favicon (src/Web/wwwroot/favicon.svg) and the docs-site favicon
    // (website/static/img/favicon.svg) must be the SAME file: a logo change has to land in both at once so
    // the running app and the published docs never show different marks.
    [Fact]
    public void App_and_docs_favicon_are_byte_identical()
    {
        var appFavicon = Path.Combine(RepoRoot, "src", "Web", "wwwroot", "favicon.svg");
        var docsFavicon = Path.Combine(RepoRoot, "website", "static", "img", "favicon.svg");

        File.Exists(appFavicon).Should().BeTrue("the app favicon is missing: {0}", appFavicon);
        File.Exists(docsFavicon).Should().BeTrue("the docs favicon is missing: {0}", docsFavicon);

        File.ReadAllBytes(appFavicon).Should().Equal(File.ReadAllBytes(docsFavicon),
            "the app and docs favicon must stay in sync — update both when the logo changes");
    }

    private static IEnumerable<string> ScanSource(string root, Regex pattern, Func<string, bool> fileFilter)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            if (!fileFilter(file))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (pattern.IsMatch(line))
                {
                    yield return $"  {Path.GetRelativePath(root, file)}:{lineNumber}";
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
}
