using System.Text.Json;

namespace IntegrationTests.CopyLive;

// Loads local, gitignored credentials for the live copy-trading tests. Everything is read from
// files under <repo>/secrets so reruns need no prompts. Returns null when a file is absent, which
// makes the live tests skip cleanly on machines without the secrets.
public static class LiveCopySecrets
{
    public const string AppFileName = "openapi-test-app.local.json";
    public const string TokensFileName = "openapi-tokens.local.json";

    public sealed record AppCredentials(string ClientId, string ClientSecret);

    public sealed record CachedAccount(long CtidTraderAccountId, long TraderLogin, bool IsLive);

    public sealed record TokenCache(
        string RefreshToken,
        string AccessToken,
        bool IsLive,
        IReadOnlyList<CachedAccount> Accounts);

    public static string SecretsDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "secrets");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the repo 'secrets' directory.");
        }
    }

    public static AppCredentials? LoadApp() => Load<AppCredentials>(AppFileName);

    public static TokenCache? LoadTokens() => Load<TokenCache>(TokensFileName);

    public static void SaveTokens(TokenCache tokens) =>
        File.WriteAllText(Path.Combine(SecretsDirectory, TokensFileName),
            JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true }));

    private static T? Load<T>(string fileName) where T : class
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "secrets", fileName);
            if (File.Exists(path)) return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            dir = dir.Parent;
        }
        return null;
    }
}
