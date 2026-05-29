using System.Text.Json;

namespace Infrastructure.Builder;

public static class Templates
{
    private const string CSharpDir = "CSharp";
    private const string PythonDir = "Python";
    private const string TemplatesRoot = "Builder/Templates";
    private const string TargetFramework = "net6.0";
    private const string PackageName = "cTrader.Automate";
    private const string PackageVersion = "*";
    private const string AlgoType = "Robots";
    private const string AdditionalContentFileName = "config.json";
    private const string ProjectFileName = "Project.csproj";
    private const string RobotCsFileName = "Robot.cs";
    private const string RobotPyFileName = "Robot.py";

    public static string CreateProjectJson(string languageName, string name)
    {
        var isPython = string.Equals(languageName, "Python", StringComparison.OrdinalIgnoreCase);
        var dir = isPython ? PythonDir : CSharpDir;
        var identityClass = SanitizeIdentifier(name);
        var templateRoot = Path.Combine(AppContext.BaseDirectory, TemplatesRoot, dir);

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(templateRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(templateRoot, path).Replace('\\', '/');
            var outName = RenameOutput(rel, name);
            var content = Substitute(File.ReadAllText(path), name, identityClass);
            files[outName] = content;
        }
        return JsonSerializer.Serialize(files);
    }

    private static string RenameOutput(string relativePath, string name) => relativePath switch
    {
        ProjectFileName => $"{name}.csproj",
        RobotCsFileName => $"{name}.cs",
        RobotPyFileName => $"{name}.py",
        _ => relativePath
    };

    private static string Substitute(string content, string name, string identityClass) =>
        content
            .Replace("{TargetFramework}", TargetFramework, StringComparison.Ordinal)
            .Replace("{PackageName}", PackageName, StringComparison.Ordinal)
            .Replace("{PackageVersion}", PackageVersion, StringComparison.Ordinal)
            .Replace("{AlgoType}", AlgoType, StringComparison.Ordinal)
            .Replace("{AdditionalContentFileName}", AdditionalContentFileName, StringComparison.Ordinal)
            .Replace("{MainCodeFileName}", name, StringComparison.Ordinal)
            .Replace("{IdentityClass}", identityClass, StringComparison.Ordinal);

    private static string SanitizeIdentifier(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var s = new string(chars);
        if (s.Length == 0 || char.IsDigit(s[0])) s = "_" + s;
        return s;
    }
}
