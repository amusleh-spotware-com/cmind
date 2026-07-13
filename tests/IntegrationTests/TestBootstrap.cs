using System.Runtime.CompilerServices;

namespace IntegrationTests;

// The economic-calendar ingestion and currency-strength refresh workers are ON by default in production
// (the E2E suite verifies that end-to-end). In the integration suite every test class boots its own web
// host in parallel, so leaving those background warm-up workers on would spin dozens of concurrent DB
// backfills and ONNX AI refreshes at once — resource contention that flakes otherwise-unrelated tests.
// Each test seeds exactly the calendar/currency data it asserts on, so switch the workers off process-wide
// before any host starts (env vars bind to App:Calendar:IngestionEnabled / App:CurrencyStrength:RefreshEnabled).
internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void DisableBackgroundWarmups()
    {
        Environment.SetEnvironmentVariable("App__Calendar__IngestionEnabled", "false");
        Environment.SetEnvironmentVariable("App__CurrencyStrength__RefreshEnabled", "false");
    }
}
