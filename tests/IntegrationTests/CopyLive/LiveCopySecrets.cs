using System.Text.Json;

namespace IntegrationTests.CopyLive;

// Loads local, gitignored credentials for the live copy-trading tests. Everything is read from
// files under <repo>/secrets so reruns need no prompts. Returns null when a file is absent, which
// makes the live tests skip cleanly on machines without the secrets.
//
// The token cache is multi-cID: one entry per authorized cID, each with its own refresh/access token
// and account list. It is written by the OAuth onboarding (E2ETests) or the volume bootstrap.
public static class LiveCopySecrets
{
    public const string AppFileName = "openapi-test-app.local.json";
    public const string TokensFileName = "openapi-tokens.local.json";

    public sealed record AppCredentials(string ClientId, string ClientSecret);

    public sealed record CachedAccount(long CtidTraderAccountId, long TraderLogin, bool IsLive);

    public sealed record CidTokens(
        string Cid, string RefreshToken, string AccessToken, bool IsLive, IReadOnlyList<CachedAccount> Accounts);

    public sealed record TokenCache(IReadOnlyList<CidTokens> Cids);

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

    // Best-effort: the refreshed access token is only cached to speed up the next local run. When the
    // secrets directory is a read-only mount (e.g. a Kubernetes Secret in the in-cluster test Job),
    // writing back is neither possible nor needed, so a write failure must not fail the run.
    public static void SaveTokens(TokenCache tokens)
    {
        try
        {
            File.WriteAllText(Path.Combine(SecretsDirectory, TokensFileName),
                JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // read-only or absent secrets mount — keep the freshly refreshed tokens in memory only.
        }
    }

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
