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

    // Standalone agent processes that ship without the Core source-generated LogMessages catalog.
    private static readonly string[] DirectLoggingExemptProjects = ["ExternalNode", "CopyEngine", "CopyAgent"];

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
