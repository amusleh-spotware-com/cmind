using System.Reflection;

namespace Core;

/// <summary>
/// Product version surfaced at runtime, read from the assembly's informational version
/// (set by <c>VersionPrefix</c> in <c>Directory.Build.props</c>). The git-hash suffix,
/// if any, is stripped so callers get a clean SemVer string.
/// </summary>
public static class VersionInfo
{
    public static string Product { get; } = ResolveProduct();

    private static string ResolveProduct()
    {
        var informational = typeof(VersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+', StringSplitOptions.RemoveEmptyEntries)[0];

        return typeof(VersionInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
