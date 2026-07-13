using System.IO;

namespace UnitTests.Localization;

// Locates the repository root at test time by walking up from the test binary until it finds the
// solution's root marker (global.json), so file-scanning tests (resx parity, the no-hardcoded-string
// gate) read the real source tree rather than copied build output.
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string WebResources => Path.Combine(Root, "src", "Web", "Resources");
    public static string WebComponents => Path.Combine(Root, "src", "Web", "Components");
    public static string LocalizationTestDir => Path.Combine(Root, "tests", "UnitTests", "Localization");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json"))
                && Directory.Exists(Path.Combine(dir.FullName, "src", "Web")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root (global.json).");
    }
}
