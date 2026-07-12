using System.Reflection;
using Core.Options;
using Core.WhiteLabel;
using FluentAssertions;
using Xunit;

namespace UnitTests.WhiteLabel;

/// <summary>
/// Enforces the "white-label options in sync" mandate: every property on a white-label options record is
/// either catalogued (so the owner can change it at runtime) or explicitly excluded with a reason. A new
/// white-label option that is not registered fails the build here.
/// </summary>
public class WhiteLabelCatalogParityTests
{
    private static readonly HashSet<Type> NestedContainers =
    [
        typeof(RegistrationCaptchaOptions),
        typeof(RegistrationAttributeOptions),
        typeof(RegistrationApiOptions)
    ];

    private static readonly HashSet<string> CataloguedPaths =
        WhiteLabelCatalog.All.Select(o => o.PropertyPath).ToHashSet(StringComparer.Ordinal);

    public static TheoryData<Type, string> Roots() => new()
    {
        { typeof(BrandingOptions), "Branding" },
        { typeof(FeaturesOptions), "Features" },
        { typeof(RegistrationOptions), "Registration" },
        { typeof(AccountsOptions), "Accounts" },
        { typeof(EmailOptions), "Email" }
    };

    [Theory]
    [MemberData(nameof(Roots))]
    public void Every_white_label_property_is_catalogued_or_excluded(Type type, string prefix)
    {
        var missing = new List<string>();
        Walk(type, prefix, missing);
        missing.Should().BeEmpty(
            "every white-label option must be registered in WhiteLabelCatalog or IntentionallyExcluded — see the CLAUDE.md mandate");
    }

    private static void Walk(Type type, string prefix, List<string> missing)
    {
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.GetIndexParameters().Length > 0) continue; // skip computed/get-only + indexers
            var path = $"{prefix}.{prop.Name}";

            if (NestedContainers.Contains(prop.PropertyType))
            {
                Walk(prop.PropertyType, path, missing);
                continue;
            }

            if (!CataloguedPaths.Contains(path) && !WhiteLabelCatalog.IntentionallyExcluded.ContainsKey(path))
                missing.Add(path);
        }
    }

    [Fact]
    public void Catalog_keys_are_unique()
    {
        var duplicates = WhiteLabelCatalog.All
            .GroupBy(o => o.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        duplicates.Should().BeEmpty();
    }

    [Fact]
    public void Every_catalog_property_path_resolves_on_AppOptions()
    {
        var options = new AppOptions();
        foreach (var option in WhiteLabelCatalog.All)
        {
            object? current = options;
            foreach (var part in option.PropertyPath.Split('.'))
            {
                current.Should().NotBeNull($"path segment before '{part}' of '{option.PropertyPath}' must resolve");
                var prop = current!.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                prop.Should().NotBeNull($"'{option.PropertyPath}' must map to a real AppOptions property");
                current = prop!.GetValue(current);
            }
        }
    }

    [Fact]
    public void Enum_options_carry_their_enum_type()
    {
        foreach (var option in WhiteLabelCatalog.All.Where(o => o.Kind == WhiteLabelValueKind.Enum))
            option.EnumType.Should().NotBeNull($"enum option '{option.Key}' must declare its EnumType");
    }

    [Fact]
    public void Node_white_label_options_are_covered()
    {
        CataloguedPaths.Should().Contain("Branding.NodesUi");
        CataloguedPaths.Should().Contain("Branding.RestrictNodesToOwner");
    }
}
