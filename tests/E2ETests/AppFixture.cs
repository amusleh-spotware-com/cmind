using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;
using Xunit;

namespace E2ETests;

public class AppFixture : IAsyncLifetime
{
    public const string OwnerEmail = "owner@e2e.local";
    public const string OwnerPassword = "Owner_Pass_123!";

    // Playwright's built-in defaults (5s for expect assertions, 30s for navigation) are too tight when
    // several heavyweight collection fixtures boot in parallel (each = a full ASP.NET app + a Postgres
    // Testcontainer + a browser): under that CPU/IO contention a slow-but-correct first render can exceed
    // the default and flake. E2E must pass on machine speed, not race it — so we set generous timeouts
    // once, globally, and per browser context below. Reliability over raw speed.
    private const int ActionTimeoutMs = 30_000;
    private const int NavigationTimeoutMs = 90_000;
    private const float ExpectTimeoutMs = 20_000f;

    // The whole (shared) collection ran against ONE app process, and every page a test opened left its
    // browser context — and its live Blazor Server SignalR circuit — alive until the collection finished.
    // Over a few hundred sequential tests those hundreds of leaked circuits starved the single app, so the
    // tests that happen to run last (the /quant/* pages, late in the alphabet) timed out waiting for a
    // slow-but-correct render. Bound the live contexts: keep only the most recent few and dispose the rest
    // as new pages are opened (safe — the collection runs sequentially, so older contexts belong to tests
    // that have already finished).
    private const int MaxLiveContexts = 6;

    [ModuleInitializer]
    internal static void ConfigurePlaywrightDefaults() => Assertions.SetDefaultExpectTimeout(ExpectTimeoutMs);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly StringBuilder _appLog = new();
    private readonly List<IBrowserContext> _contexts = [];
    private Process? _app;
    private IPlaywright? _playwright;

    public string AppLog => _appLog.ToString();
    public string BaseUrl { get; private set; } = "";
    public IBrowser Browser { get; private set; } = default!;
    public string StorageState { get; private set; } = "";

    // A specialized fixture can decline to boot (e.g. an opt-in lane whose prerequisite env is absent),
    // so the collection's tests skip cheaply without paying the app/Postgres startup cost.
    protected virtual bool ShouldStart => true;

    public async Task InitializeAsync()
    {
        if (!ShouldStart) return;
        try
        {
            await _postgres.StartAsync();
            await OnBeforeStartAsync();

            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            StartApp(port);
            await WaitForLoginReadyAsync();

            _playwright = await Playwright.CreateAsync();
            Browser = await LaunchBrowserAsync();
            StorageState = await LogInAndCaptureStateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try { await OnDisposeAsync(); }
        catch { /* best effort */ }

        foreach (var context in _contexts)
        {
            try { await context.DisposeAsync(); }
            catch { /* best effort */ }
        }
        _contexts.Clear();

        try { if (Browser is not null) await Browser.DisposeAsync(); }
        catch { /* best effort */ }
        finally { _playwright?.Dispose(); }

        if (_app is { HasExited: false })
        {
            try { _app.Kill(entireProcessTree: true); }
            catch { /* best effort */ }
        }
        _app?.Dispose();

        await _postgres.DisposeAsync();
    }

    // Extension hooks for a specialized fixture (e.g. one that boots a fake local-LLM endpoint and
    // configures the app to use it). Base behaviour is a no-op, so the default fixture is unchanged.
    protected virtual Task OnBeforeStartAsync() => Task.CompletedTask;
    protected virtual void ConfigureApp(ProcessStartInfo psi) { }
    protected virtual Task OnDisposeAsync() => Task.CompletedTask;

    private void StartApp(int port)
    {
        var webDll = typeof(Program).Assembly.Location;
        var workDir = Path.GetDirectoryName(webDll)!;

        var psi = new ProcessStartInfo("dotnet", $"\"{webDll}\"")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ConnectionStrings__appdb"] = _postgres.GetConnectionString();
        psi.Environment["App__OwnerEmail"] = OwnerEmail;
        psi.Environment["App__OwnerPassword"] = OwnerPassword;
        // The shipped built-in AI is on by default; the base fixture keeps it OFF so the keyless
        // "not configured" gate tests stay valid. Specialized fixtures opt back in as needed.
        psi.Environment["App__Ai__BuiltIn__Enabled"] = "false";
        // Calendar ingestion and currency-strength refresh warm-up workers are ON by default in
        // production, but with no FRED/BLS/AI source configured here they can only wake, back off and
        // retry — producing no data the suite asserts on (every calendar/currency E2E verifies the
        // source-less/keyless GATED path, which is identical whether the workers run) while churning CPU
        // and DB against the single shared app for the whole ~9-minute run. That background load
        // intermittently starved the circuit and flaked otherwise-unrelated tests, so keep them OFF here
        // (mirrors IntegrationTests' TestBootstrap).
        psi.Environment["App__Calendar__IngestionEnabled"] = "false";
        psi.Environment["App__CurrencyStrength__RefreshEnabled"] = "false";
        ConfigureApp(psi);

        _app = new Process { StartInfo = psi };
        _app.OutputDataReceived += (_, e) => { if (e.Data is not null) _appLog.AppendLine(e.Data); };
        _app.ErrorDataReceived += (_, e) => { if (e.Data is not null) _appLog.AppendLine(e.Data); };
        _app.Start();
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();
    }

    private async Task WaitForLoginReadyAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(2))
        {
            if (_app is { HasExited: true })
                throw new InvalidOperationException($"Web app exited early.\n{_appLog}");
            try
            {
                var r = await http.PostAsJsonAsync("/api/auth/login",
                    new { Email = OwnerEmail, Password = OwnerPassword, RememberMe = true });
                if (r.StatusCode == HttpStatusCode.OK)
                {
                    // Warm the post-login landing route ("/", the heavy dashboard: Razor SSR + ApexCharts +
                    // a live SignalR circuit) with the freshly-issued auth cookie, so its FIRST server render
                    // is JIT-compiled here — before the browser logs in and navigates there. Otherwise that
                    // cold first render can exceed the navigation timeout and flake fixture init (seen when a
                    // collection runs solo right after a rebuild). Best-effort: readiness never blocks on it.
                    try
                    {
                        using var warm = new HttpRequestMessage(HttpMethod.Get, "/");
                        if (r.Headers.TryGetValues("Set-Cookie", out var cookies)
                            && cookies.FirstOrDefault() is { } cookie)
                            warm.Headers.Add("Cookie", cookie.Split(';')[0]);
                        await http.SendAsync(warm);
                    }
                    catch { /* best-effort dashboard warm-up */ }
                    return;
                }
            }
            catch { /* not up yet */ }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Web app not ready within timeout.\n{_appLog}");
    }

    private async Task<IBrowser> LaunchBrowserAsync()
    {
        // Windows: prefer system Edge; fall back to bundled Chromium if Edge is unavailable.
        if (OperatingSystem.IsWindows())
        {
            try
            {
                Install("msedge");
                return await _playwright!.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
            }
            catch { /* fall back to Chromium */ }
        }

        Install("chromium");
        return await _playwright!.Chromium.LaunchAsync(new() { Headless = true });
    }

    private static readonly Lock InstallGate = new();
    private static readonly HashSet<string> InstalledTargets = [];

    // Playwright's `install` shells out to a Node CLI (seconds). Every collection fixture boots its own
    // app, so without memoization this ran once per collection (~9×) even though the browser only needs
    // installing once per test-run process. Install each target at most once; the lock serializes the
    // first-time install so parallel collections don't race the same files.
    private static void Install(string target)
    {
        lock (InstallGate)
        {
            if (!InstalledTargets.Add(target)) return;
            var exit = Microsoft.Playwright.Program.Main(["install", target]);
            if (exit != 0)
            {
                InstalledTargets.Remove(target);
                throw new InvalidOperationException($"Playwright install '{target}' failed with exit code {exit}.");
            }
        }
    }

    // Applies the generous timeouts to a context so every navigation/action/expect on it survives
    // parallel-boot contention, then tracks it for disposal.
    private void Track(IBrowserContext context)
    {
        context.SetDefaultTimeout(ActionTimeoutMs);
        context.SetDefaultNavigationTimeout(NavigationTimeoutMs);
        _contexts.Add(context);
    }

    // Dispose the oldest tracked contexts (and their circuits) once we exceed the cap, keeping only the
    // most recent few alive. Called as each new page is opened so resource use stays bounded across a long
    // sequential collection instead of growing to hundreds of live circuits.
    private async Task EvictStaleContextsAsync()
    {
        while (_contexts.Count > MaxLiveContexts)
        {
            var oldest = _contexts[0];
            _contexts.RemoveAt(0);
            try { await oldest.DisposeAsync(); }
            catch { /* best effort — a context from a finished test */ }
        }
    }

    private async Task<string> LogInAndCaptureStateAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl });
        context.SetDefaultTimeout(ActionTimeoutMs);
        context.SetDefaultNavigationTimeout(NavigationTimeoutMs);
        var page = await context.NewPageAsync();
        await page.GotoAsync("/login");
        await page.FillAsync("input[name=Email]", OwnerEmail);
        await page.FillAsync("input[name=Password]", OwnerPassword);
        await page.ClickAsync("button.app-login-button");
        // Wait only for the navigation to "/" to COMMIT, not for the full Load event. The auth cookie is
        // set on the login redirect (before "/" is fetched), so a committed navigation is enough to capture
        // the storage state — and it avoids depending on the heavy dashboard ("/" has ApexCharts + a live
        // SignalR circuit) finishing Load, which on a cold post-build start could exceed the nav timeout and
        // flake fixture init (observed: a collection run solo right after a rebuild timing out here).
        await page.WaitForURLAsync($"{BaseUrl}/", new() { WaitUntil = WaitUntilState.Commit });
        var state = await context.StorageStateAsync();
        await context.DisposeAsync();
        return state;
    }

    public async Task<IPage> NewAuthedPageAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl, StorageState = StorageState });
        Track(context);
        await EvictStaleContextsAsync();
        return await context.NewPageAsync();
    }

    // Authenticated page emulating a real mobile device (viewport, touch, DPR, user-agent) from
    // Playwright's device registry, e.g. "iPhone 13", "Pixel 5". Reuses the owner storage state.
    public async Task<IPage> NewAuthedMobilePageAsync(string device = "iPhone 13")
    {
        var options = _playwright!.Devices[device];
        options.BaseURL = BaseUrl;
        options.StorageState = StorageState;
        var context = await Browser.NewContextAsync(options);
        Track(context);
        await EvictStaleContextsAsync();
        return await context.NewPageAsync();
    }

    // Anonymous (signed-out) page — for the login screen and auth redirects.
    public async Task<IPage> NewAnonymousPageAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl });
        Track(context);
        await EvictStaleContextsAsync();
        return await context.NewPageAsync();
    }

    // Anonymous page on emulated mobile hardware.
    public async Task<IPage> NewAnonymousMobilePageAsync(string device = "iPhone 13")
    {
        var options = _playwright!.Devices[device];
        options.BaseURL = BaseUrl;
        var context = await Browser.NewContextAsync(options);
        Track(context);
        await EvictStaleContextsAsync();
        return await context.NewPageAsync();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
