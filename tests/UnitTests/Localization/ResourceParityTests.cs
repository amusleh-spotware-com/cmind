using System.IO;
using System.Xml.Linq;
using Core.Constants;
using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

// Guarantees the promise of the localization mandate: every language ships every key, with a real value.
// A missing key would leave a user staring at English (or a raw key) inside an otherwise-translated app;
// an empty value would blank a control. Both fail the build here, so translations can never drift behind
// the base resource set.
public class ResourceParityTests
{
    private static readonly string BaseFile = Path.Combine(RepoPaths.WebResources, "Ui.resx");

    private static IReadOnlyDictionary<string, string> ReadResx(string path)
    {
        File.Exists(path).Should().BeTrue($"resource file missing: {path}");
        return XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")?.Value ?? "");
    }

    public static IEnumerable<object[]> NonDefaultCultures() =>
        SupportedCultures.All
            .Where(c => c != SupportedCultures.Default)
            .Select(c => new object[] { c });

    [Fact]
    public void Base_resource_set_is_non_empty()
        => ReadResx(BaseFile).Should().NotBeEmpty();

    [Theory]
    [MemberData(nameof(NonDefaultCultures))]
    public void Every_culture_has_exactly_the_base_keys_all_non_empty(string culture)
    {
        var baseKeys = ReadResx(BaseFile).Keys.ToHashSet();
        var path = Path.Combine(RepoPaths.WebResources, $"Ui.{culture}.resx");
        var entries = ReadResx(path);

        entries.Keys.Should().BeEquivalentTo(baseKeys,
            $"culture '{culture}' must define exactly the base keys — no missing, no extra");

        foreach (var (key, value) in entries)
            value.Should().NotBeNullOrWhiteSpace($"key '{key}' in culture '{culture}' must have a real translation");
    }

    [Fact]
    public void A_resx_file_exists_for_every_supported_non_default_culture()
    {
        foreach (var culture in SupportedCultures.All.Where(c => c != SupportedCultures.Default))
            File.Exists(Path.Combine(RepoPaths.WebResources, $"Ui.{culture}.resx"))
                .Should().BeTrue($"missing resource file for supported culture '{culture}'");
    }
}
