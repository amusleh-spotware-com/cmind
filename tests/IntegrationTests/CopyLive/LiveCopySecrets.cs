using System.Text.Json;

namespace IntegrationTests.CopyLive;

// Loads local, gitignored credentials for the live copy-trading tests. Every value is read from a
// single unified file, secrets/dev-credentials.local.json (copy dev-credentials.example.json from the
// repo root and fill it in) — OpenAPI app/tokens, owner login, AI key and the economic-calendar source
// keys (Calendar.FredApiKey / Calendar.BlsApiKey). The legacy split files (openapi-test-app.local.json / openapi-tokens
// .local.json) are still honoured as a fallback so existing machines keep working. Returns null when a
// value is absent, which makes the live tests skip cleanly on machines without the secrets.
public static class LiveCopySecrets
{
    public const string DevCredentialsFileName = "dev-credentials.local.json";
    public const string AppFileName = "openapi-test-app.local.json";
    public const string TokensFileName = "openapi-tokens.local.json";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public sealed record AppCredentials(string ClientId, string ClientSecret);

    public sealed record CachedAccount(long CtidTraderAccountId, long TraderLogin, bool IsLive);

    public sealed record CidTokens(
        string Cid, string RefreshToken, string AccessToken, bool IsLive, IReadOnlyList<CachedAccount> Accounts);

    public sealed record TokenCache(IReadOnlyList<CidTokens> Cids);

    public sealed record CidLogin(string Cid, string Username, string Password);

    public sealed record OwnerSection(string? Email, string? Password);

    public sealed record DatabaseSection(string? ConnectionString);

    public sealed record AiSection(string? ApiKey);

    public sealed record CalendarSection(string? FredApiKey, string? BlsApiKey);

    public sealed record OpenApiSection(AppCredentials? App, IReadOnlyList<CidLogin>? Cids, TokenCache? Tokens);

    public sealed record DevCredentials(
        OpenApiSection? OpenApi, OwnerSection? Owner, DatabaseSection? Database, AiSection? Ai, CalendarSection? Calendar);

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

    public static DevCredentials? LoadDevCredentials()
    {
        var path = FindPath(DevCredentialsFileName);
        return path is null ? null : JsonSerializer.Deserialize<DevCredentials>(File.ReadAllText(path), ReadOptions);
    }

    public static AppCredentials? LoadApp()
        => LoadDevCredentials()?.OpenApi?.App ?? Load<AppCredentials>(AppFileName);

    public static TokenCache? LoadTokens()
        => LoadDevCredentials()?.OpenApi?.Tokens ?? Load<TokenCache>(TokensFileName);

    public static IReadOnlyList<CidLogin> LoadCids()
        => LoadDevCredentials()?.OpenApi?.Cids ?? [];

    public static string? LoadAiApiKey() => LoadDevCredentials()?.Ai?.ApiKey;

    public static string? LoadFredApiKey() => LoadDevCredentials()?.Calendar?.FredApiKey;

    public static string? LoadBlsApiKey() => LoadDevCredentials()?.Calendar?.BlsApiKey;

    public static OwnerSection? LoadOwner() => LoadDevCredentials()?.Owner;

    // Best-effort: the refreshed access token is only cached to speed up the next local run. When the
    // secrets directory is a read-only mount (e.g. a Kubernetes Secret in the in-cluster test Job),
    // writing back is neither possible nor needed, so a write failure must not fail the run.
    public static void SaveTokens(TokenCache tokens)
    {
        try
        {
            var unified = FindPath(DevCredentialsFileName);
            if (unified is not null)
            {
                var dev = LoadDevCredentials() ?? new DevCredentials(null, null, null, null, null);
                var openApi = (dev.OpenApi ?? new OpenApiSection(null, null, null)) with { Tokens = tokens };
                File.WriteAllText(unified, JsonSerializer.Serialize(dev with { OpenApi = openApi }, WriteOptions));
                return;
            }

            File.WriteAllText(Path.Combine(SecretsDirectory, TokensFileName),
                JsonSerializer.Serialize(tokens, WriteOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // read-only or absent secrets mount — keep the freshly refreshed tokens in memory only.
        }
    }

    private static T? Load<T>(string fileName) where T : class
    {
        var path = FindPath(fileName);
        return path is null ? null : JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOptions);
    }

    private static string? FindPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "secrets", fileName);
            if (File.Exists(path)) return path;
            dir = dir.Parent;
        }
        return null;
    }
}
