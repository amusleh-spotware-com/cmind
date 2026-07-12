using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

// THE GATE. Fails the build if a scanned .razor file carries a hard-coded, user-facing string instead of
// an @L["key"] lookup — making it impossible to merge un-localized UI in the covered scope. The scanned
// set is the fully-migrated app shell; it grows as pages migrate (add the file to MigratedRazorFiles).
// Detection is deliberately conservative (only same-line text nodes + a curated set of text-bearing
// attributes, with code/comments/style/script stripped) so it never flags legitimate markup, yet the
// self-tests below prove it still catches a real hard-coded string.
public partial class NoHardcodedUiTextTests
{
    // Paths relative to src/Web/Components. App.razor is intentionally excluded: it is the document shell
    // (inline <script>/<style> and meta attributes), not a component surface.
    public static IEnumerable<object[]> MigratedRazorFiles() => new[]
    {
        "Layout/MainLayout.razor",
        "Layout/NavMenu.razor",
        "Layout/BottomNav.razor",
        "Dialogs/SettingsDialog.razor",
        "LanguageSwitcher.razor",
    }.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(MigratedRazorFiles))]
    public void Migrated_razor_files_have_no_hardcoded_user_facing_text(string relativePath)
    {
        var path = Path.Combine(RepoPaths.WebComponents, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"scanned file not found: {path}");

        var violations = ScanForHardcodedText(File.ReadAllText(path));
        violations.Should().BeEmpty(
            $"'{relativePath}' must use @L[\"key\"] for every user-facing string — found: {string.Join(" | ", violations)}");
    }

    [Fact]
    public void Scanner_flags_a_hardcoded_text_node()
        => ScanForHardcodedText("<MudText>Hello world</MudText>").Should().ContainSingle();

    [Fact]
    public void Scanner_flags_a_hardcoded_text_bearing_attribute()
        => ScanForHardcodedText("<MudIconButton aria-label=\"Close settings\" />").Should().ContainSingle();

    [Fact]
    public void Scanner_accepts_localized_text_and_attributes()
    {
        ScanForHardcodedText("<MudText>@L[\"nav.run\"]</MudText>").Should().BeEmpty();
        ScanForHardcodedText("<MudIconButton aria-label=\"@L[\\\"settings.close\\\"]\" />").Should().BeEmpty();
        ScanForHardcodedText("<MudText>v@(Core.VersionInfo.Product)</MudText>").Should().BeEmpty();
    }

    // Returns a human-readable violation per offending occurrence; empty means the markup is clean.
    private static List<string> ScanForHardcodedText(string content)
    {
        // Only the markup region — a component's @code {} C# block is not a localization surface.
        var codeIndex = content.IndexOf("@code", StringComparison.Ordinal);
        var markup = codeIndex >= 0 ? content[..codeIndex] : content;

        // Strip Razor comments, <style> and <script> blocks so their letters never look like UI text.
        markup = RazorCommentRegex().Replace(markup, " ");
        markup = StyleOrScriptRegex().Replace(markup, " ");

        var violations = new List<string>();
        foreach (var line in markup.Split('\n'))
        {
            foreach (Match m in TextNodeRegex().Matches(line))
            {
                var text = m.Groups[1].Value.Trim();
                if (IsNaturalLanguage(text)) violations.Add($"text node \"{text}\"");
            }

            foreach (Match m in TextAttributeRegex().Matches(line))
            {
                var value = m.Groups[2].Value.Trim();
                if (IsNaturalLanguage(value)) violations.Add($"{m.Groups[1].Value}=\"{value}\"");
            }
        }
        return violations;
    }

    // A run of real words with no code punctuation — i.e. something a translator would own. Anything with
    // Razor/C# syntax (@, brackets, parens, =, ;, /, |, quotes) is code, not a hard-coded string.
    private static bool IsNaturalLanguage(string text) =>
        text.Length > 0
        && WordRegex().IsMatch(text)
        && !CodePunctuationRegex().IsMatch(text);

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorCommentRegex();

    [GeneratedRegex(@"<(style|script)\b[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StyleOrScriptRegex();

    // Same-line text between tags whose first non-space char is a letter (an @-expression starts with '@'
    // and is skipped). Cross-line content never matches, which avoids flagging C# between components.
    [GeneratedRegex(@">\s*([A-Za-z][^<>]*?)\s*<")]
    private static partial Regex TextNodeRegex();

    [GeneratedRegex("\\b(Label|Placeholder|HelperText|Title|Text|AriaLabel|aria-label|Alt|alt)\\s*=\\s*\"(?!@)([^\"]*)\"")]
    private static partial Regex TextAttributeRegex();

    [GeneratedRegex(@"[A-Za-z]{2,}")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"[@{}\[\]()=;/\\|""<>]")]
    private static partial Regex CodePunctuationRegex();
}
