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
        var dir = Path.Combine(FindSecretsDir(), "openapi-tokens.local.json");
        File.WriteAllText(dir, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static (string ClientId, string ClientSecret)? LoadApp()
    {
        var path = Find("openapi-test-app.local.json");
        if (path is null) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return (doc.RootElement.GetProperty("ClientId").GetString()!, doc.RootElement.GetProperty("ClientSecret").GetString()!);
    }

    private static IReadOnlyList<(string Cid, string Username, string Password)> LoadCids()
    {
        var path = Find("openapi-cids.local.json");
        if (path is null) return [];
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("Cids").EnumerateArray()
            .Select(c => (c.GetProperty("Cid").GetString()!, c.GetProperty("Username").GetString()!, c.GetProperty("Password").GetString()!))
            .ToList();
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
