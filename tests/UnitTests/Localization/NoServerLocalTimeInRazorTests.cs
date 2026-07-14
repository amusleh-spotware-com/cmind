using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

// Census gate (mandate 12) for the time-zone rule. In Blazor Server, DateTimeOffset.ToLocalTime() and
// DateTime(Offset).LocalDateTime bind the SERVER host's zone, so every user would see the server's clock
// instead of their own. Every human-facing time must render through the user's zone (the <UserTime>
// component or the TimeDisplay.ToUserString helper, both fed by IUserTimeZone). This scans every .razor and
// fails the build on a server-local conversion — the machine-enforced guarantee that all shown times align
// to the user's zone.
public sealed partial class NoServerLocalTimeInRazorTests
{
    [Fact]
    public void No_component_converts_a_time_to_the_server_local_zone()
    {
        var root = RepoPaths.WebComponents;
        var offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => ServerLocalTimeRegex().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order()
            .ToArray();

        offenders.Should().BeEmpty(
            "these components render a time in the SERVER zone — use <UserTime> or .ToUserString(zone, ...) "
            + "via IUserTimeZone so times show in the user's zone (mandate 11/12):\n  {0}",
            string.Join("\n  ", offenders));
    }

    // .ToLocalTime(  or  .LocalDateTime  — both resolve the server host zone in Blazor Server.
    [GeneratedRegex(@"\.\s*(ToLocalTime\s*\(|LocalDateTime\b)")]
    private static partial Regex ServerLocalTimeRegex();
}
