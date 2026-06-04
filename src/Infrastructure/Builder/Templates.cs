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
        var identifier = ToPascalCaseIdentifier(name);
        var templateRoot = Path.Combine(AppContext.BaseDirectory, TemplatesRoot, dir);

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(templateRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(templateRoot, path).Replace('\\', '/');
            var outName = RenameOutput(rel, identifier);
            var content = Substitute(File.ReadAllText(path), identifier);
            files[outName] = content;
        }
        return JsonSerializer.Serialize(files);
    }

    private static string RenameOutput(string relativePath, string identifier) => relativePath switch
    {
        ProjectFileName => $"{identifier}.csproj",
        RobotCsFileName => $"{identifier}.cs",
        RobotPyFileName => $"{identifier}.py",
        _ => relativePath
    };

    private static string Substitute(string content, string identifier) =>
        content
            .Replace("{TargetFramework}", TargetFramework, StringComparison.Ordinal)
            .Replace("{PackageName}", PackageName, StringComparison.Ordinal)
            .Replace("{PackageVersion}", PackageVersion, StringComparison.Ordinal)
            .Replace("{AlgoType}", AlgoType, StringComparison.Ordinal)
            .Replace("{AdditionalContentFileName}", AdditionalContentFileName, StringComparison.Ordinal)
            .Replace("{MainCodeFileName}", identifier, StringComparison.Ordinal)
            .Replace("{IdentityClass}", identifier, StringComparison.Ordinal);

    private static string ToPascalCaseIdentifier(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);
        var capitalizeNext = true;
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c))
            {
                capitalizeNext = true;
                continue;
            }
            builder.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }
        if (builder.Length == 0 || char.IsDigit(builder[0]))
            builder.Insert(0, '_');
        return builder.ToString();
    }
}
