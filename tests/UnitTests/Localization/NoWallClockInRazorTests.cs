using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

// Mandate 4 gate for the markup surface. The analyzer sweep flags DateTime.UtcNow/.Now in .cs, but NOT
// inside .razor markup expressions (A-02 shipped `@DateTime.UtcNow.Year` in Login.razor). This census
// scans every component for a real-clock read and fails the build — inject TimeProvider and read
// GetUtcNow() instead. (Comments and the @code scanner exclusions keep false positives out.)
public sealed partial class NoWallClockInRazorTests
{
    [Fact]
    public void No_component_reads_the_real_clock_directly()
    {
        var root = RepoPaths.WebComponents;
        var offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => WallClockRegex().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order()
            .ToArray();

        offenders.Should().BeEmpty(
            "these components read the real clock — inject TimeProvider and use GetUtcNow() (mandate 4):\n  {0}",
            string.Join("\n  ", offenders));
    }

    // DateTime.UtcNow / DateTime.Now / DateTimeOffset.UtcNow / DateTimeOffset.Now — but not GetUtcNow().
    [GeneratedRegex(@"\bDateTime(Offset)?\s*\.\s*(UtcNow|Now)\b")]
    private static partial Regex WallClockRegex();
}
