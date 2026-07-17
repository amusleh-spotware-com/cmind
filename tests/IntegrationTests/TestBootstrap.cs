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

        // The shipped built-in AI (ONNX) is on by default, but no model is installed in the test
        // environment — so an AI call would return "model not installed" instead of the keyless
        // "AI is not configured" degraded result the disabled-path tests assert. Keep the built-in
        // provider OFF process-wide (mirrors the E2E AppFixture) so those gates are deterministic.
        Environment.SetEnvironmentVariable("App__Ai__BuiltIn__Enabled", "false");

        // Copy hosting is gated on the single CopyTrading feature flag (on by default), so the copy API and
        // UI stay available for the copy tests — but the background supervisor would otherwise reconcile
        // every 30s against each parallel host and, for a linkable running profile, open a real cTrader
        // socket. A long reconcile interval keeps it inert for the run (one boot-time reconcile with no
        // running profiles, then it sleeps past the suite) without disabling the feature.
        Environment.SetEnvironmentVariable("App__Copy__ReconcileInterval", "01:00:00");
    }
}
