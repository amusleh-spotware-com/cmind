using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests.CopyLive;

// Live OAuth onboarding runner. Gated on CMIND_ONBOARD=1 (drives a real browser against cTrader ID).
// Run this once per machine after saving the cID credentials; it authorizes every cID via Open API and
// writes the multi-cID token cache the live copy tests consume. Refresh tokens do not expire, so after
// this the live suite runs with no browser and no dev interaction.
public sealed class OnboardingTests(ITestOutputHelper output)
{
    private const string ScreenshotDir = "C:/Users/afhac/source/cMind/tmp-onboard";

    [Fact]
    public async Task Onboard_all_cids_and_write_token_cache()
    {
        if (Environment.GetEnvironmentVariable("CMIND_ONBOARD") != "1") return;

        var app = LoadApp();
        var cids = LoadCids();
        if (app is null || cids.Count == 0) { output.WriteLine("missing app/cid secrets"); return; }

        Directory.CreateDirectory(ScreenshotDir);
        using var pw = await Playwright.CreateAsync();
        Microsoft.Playwright.Program.Main(["install", "msedge"]);
        await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });

        var onboarding = new OpenApiOnboarding(app.Value.ClientId, app.Value.ClientSecret);
        var results = new List<OpenApiOnboarding.OnboardedCid>();
        foreach (var cid in cids)
        {
            var result = await onboarding.OnboardAsync(browser, cid.Cid, cid.Username, cid.Password, ScreenshotDir);
            output.WriteLine($"onboarded cid={result.Cid} accounts={result.Accounts.Count} live={result.IsLive}");
            result.Accounts.Should().NotBeEmpty();
            results.Add(result);
        }

        WriteCache(results);
        output.WriteLine($"wrote token cache with {results.Count} cID(s) / {results.Sum(r => r.Accounts.Count)} accounts");
    }

    private static void WriteCache(IReadOnlyList<OpenApiOnboarding.OnboardedCid> results)
    {
        var cache = new
        {
            Cids = results.Select(r => new
            {
                r.Cid,
                r.RefreshToken,
                r.AccessToken,
                r.IsLive,
                Accounts = r.Accounts.Select(a => new { a.CtidTraderAccountId, a.TraderLogin, a.IsLive })
            })
        };
        var indented = new JsonSerializerOptions { WriteIndented = true };

        // Write the tokens into the single source of truth, dev-credentials.local.json (OpenApi.Tokens).
        // Onboarding loaded its app credentials from that same file, so it exists here; if it somehow does
        // not, create it under secrets/ with just the tokens.
        var unified = Find("dev-credentials.local.json");
        if (unified is not null)
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(unified))!.AsObject();
            var openApi = root["OpenApi"]?.AsObject() ?? [];
            openApi["Tokens"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(cache));
            root["OpenApi"] = openApi;
            File.WriteAllText(unified, root.ToJsonString(indented));
            return;
        }

        var fresh = new System.Text.Json.Nodes.JsonObject
        {
            ["OpenApi"] = new System.Text.Json.Nodes.JsonObject
            {
                ["Tokens"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(cache))
            }
        };
        File.WriteAllText(Path.Combine(FindSecretsDir(), "dev-credentials.local.json"), fresh.ToJsonString(indented));
    }

    private static (string ClientId, string ClientSecret)? LoadApp()
    {
        var unified = Find("dev-credentials.local.json");
        if (unified is null) return null;
        using var dev = JsonDocument.Parse(File.ReadAllText(unified));
        if (dev.RootElement.TryGetProperty("OpenApi", out var oa) && oa.TryGetProperty("App", out var app)
            && app.TryGetProperty("ClientId", out var clientId) && !string.IsNullOrWhiteSpace(clientId.GetString()))
            return (clientId.GetString()!, app.GetProperty("ClientSecret").GetString()!);
        return null;
    }

    private static IReadOnlyList<(string Cid, string Username, string Password)> LoadCids()
    {
        var unified = Find("dev-credentials.local.json");
        if (unified is null) return [];
        using var dev = JsonDocument.Parse(File.ReadAllText(unified));
        if (dev.RootElement.TryGetProperty("OpenApi", out var oa) && oa.TryGetProperty("Cids", out var cids)
            && cids.ValueKind == JsonValueKind.Array && cids.GetArrayLength() > 0)
            return cids.EnumerateArray()
                .Where(c => !string.IsNullOrWhiteSpace(c.GetProperty("Cid").GetString()))
                .Select(c => (c.GetProperty("Cid").GetString()!, c.GetProperty("Username").GetString()!, c.GetProperty("Password").GetString()!))
                .ToList();
        return [];
    }

    private static string FindSecretsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "secrets");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("secrets directory not found");
    }

    private static string? Find(string fileName)
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
