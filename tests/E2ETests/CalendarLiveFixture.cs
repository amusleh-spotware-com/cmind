using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace E2ETests;

// An app fixture with a REAL economic-calendar data source (FRED and/or BLS) configured and the ingestion
// worker enabled, so the calendar's WORKING path — the one the keyless EconomicCalendarE2ETests cannot
// reach — is driven end-to-end through the real UI and the JWT cBot API against live provider data
// (mandate 11: assert the working path when the dependency is present, not only the gated path).
//
// Keys come from the unified dev-credentials file (secrets/dev-credentials.local.json →
// Calendar.FredApiKey / Calendar.BlsApiKey), with the FRED_API_KEY / BLS_API_KEY environment variables
// taking precedence — the same contract as the live integration CalendarSourceLiveTests. When no key is
// present the fixture declines to boot (ShouldStart=false) and every test in the collection skips cleanly,
// so CI stays green while the path is still covered whenever a dev (or the cluster's mounted Secret)
// supplies a key.
public sealed class CalendarLiveFixture : AppFixture
{
    public string? FredApiKey { get; } = LoadKey("FRED_API_KEY", "FredApiKey");
    public string? BlsApiKey { get; } = LoadKey("BLS_API_KEY", "BlsApiKey");

    public bool HasSource => !string.IsNullOrWhiteSpace(FredApiKey) || !string.IsNullOrWhiteSpace(BlsApiKey);

    protected override bool ShouldStart => HasSource;

    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        // Turn the ingestion worker back on (the base fixture disables it) and hand it the real keys so it
        // fetches live releases from FRED/BLS into the calendar the tests read.
        psi.Environment["App__Calendar__IngestionEnabled"] = "true";
        if (!string.IsNullOrWhiteSpace(FredApiKey)) psi.Environment["App__Calendar__FredApiKey"] = FredApiKey;
        if (!string.IsNullOrWhiteSpace(BlsApiKey)) psi.Environment["App__Calendar__BlsApiKey"] = BlsApiKey;
        // Poll fast so the first ingested releases land well inside the JWT-API polling window.
        psi.Environment["App__Calendar__ReleasePollInterval"] = "00:00:05";
    }

    private static string? LoadKey(string envVar, string jsonProperty)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        var path = FindSecret("dev-credentials.local.json");
        if (path is null) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.TryGetProperty("Calendar", out var calendar)
               && calendar.TryGetProperty(jsonProperty, out var key)
               && !string.IsNullOrWhiteSpace(key.GetString())
            ? key.GetString()
            : null;
    }

    private static string? FindSecret(string fileName)
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

[CollectionDefinition(Name)]
public sealed class CalendarLiveCollection : ICollectionFixture<CalendarLiveFixture>
{
    public const string Name = "calendar-live";
}
