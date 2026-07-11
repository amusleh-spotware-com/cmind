using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;
using Xunit;

namespace E2ETests;

public sealed class AppFixture : IAsyncLifetime
{
    public const string OwnerEmail = "owner@e2e.local";
    public const string OwnerPassword = "Owner_Pass_123!";

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

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();

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
                if (r.StatusCode == HttpStatusCode.OK) return;
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

    private static void Install(string target)
    {
        var exit = Microsoft.Playwright.Program.Main(["install", target]);
        if (exit != 0) throw new InvalidOperationException($"Playwright install '{target}' failed with exit code {exit}.");
    }

    private async Task<string> LogInAndCaptureStateAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/login");
        await page.FillAsync("input[name=Email]", OwnerEmail);
        await page.FillAsync("input[name=Password]", OwnerPassword);
        await page.ClickAsync("button.app-login-button");
        await page.WaitForURLAsync($"{BaseUrl}/");
        var state = await context.StorageStateAsync();
        await context.DisposeAsync();
        return state;
    }

    public async Task<IPage> NewAuthedPageAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl, StorageState = StorageState });
        _contexts.Add(context);
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
        _contexts.Add(context);
        return await context.NewPageAsync();
    }

    // Anonymous (signed-out) page — for the login screen and auth redirects.
    public async Task<IPage> NewAnonymousPageAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl });
        _contexts.Add(context);
        return await context.NewPageAsync();
    }

    // Anonymous page on emulated mobile hardware.
    public async Task<IPage> NewAnonymousMobilePageAsync(string device = "iPhone 13")
    {
        var options = _playwright!.Devices[device];
        options.BaseURL = BaseUrl;
        var context = await Browser.NewContextAsync(options);
        _contexts.Add(context);
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
