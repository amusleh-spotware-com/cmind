using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CTraderOpenApi;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using CTraderOpenApi.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace E2ETests.CopyLive;

// Automates a full cTrader Open API OAuth authorization from a saved cID username/password: drives the
// cTrader ID login + app consent in a headless browser, captures the callback code (by intercepting the
// registered redirect), exchanges it for tokens, and loads the account list. No local server and no dev
// interaction — the dev only saves the cID credentials once.
public sealed class OpenApiOnboarding(string clientId, string clientSecret)
{
    private const string RedirectUri = "https://localhost:7080/openapi/callback";

    public async Task<OnboardedCid> OnboardAsync(IBrowser browser, string cid, string username, string password,
        string? screenshotDir = null)
    {
        var authorizeUrl =
            "https://openapi.ctrader.com/apps/auth?" +
            $"client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            "&scope=trading&product=web";

        var codeSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var callbackServer = await CallbackServer.StartAsync(codeSource);

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            Locale = "en-US"
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(authorizeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await DismissCookiesAsync(page);
        await Shot(page, screenshotDir, cid, "01-login");

        await page.FillAsync("input[name='id']", username);
        var passwordField = page.Locator("form:has(input[name='id']) input[name='password']").First;
        await passwordField.FillAsync(password);
        await Shot(page, screenshotDir, cid, "02-filled");
        await passwordField.PressAsync("Enter");

        await ApproveConsentIfPresentAsync(page, codeSource.Task);
        await Shot(page, screenshotDir, cid, "03-after");

        var code = await codeSource.Task.WaitAsync(TimeSpan.FromSeconds(45));
        await context.DisposeAsync();

        using var http = new HttpClient { BaseAddress = new Uri(Core.Constants.OpenApiEndpoints.AuthBaseUrl) };
        var tokens = await new OpenApiTokenClient(http)
            .ExchangeCodeAsync(clientId, clientSecret, code, RedirectUri, CancellationToken.None);

        var grant = await new OpenApiClient(new OnboardConnectionFactory())
            .LoadGrantAsync(clientId, clientSecret, tokens.AccessToken, CancellationToken.None);

        var isLive = grant.Accounts.Count > 0 && grant.Accounts.Any(a => a.IsLive);
        var accounts = grant.Accounts.Select(a => new OnboardedAccount(a.CtidTraderAccountId, a.TraderLogin, a.IsLive)).ToList();
        return new OnboardedCid(cid, tokens.RefreshToken, tokens.AccessToken, isLive, accounts);
    }

    private static async Task DismissCookiesAsync(IPage page)
    {
        foreach (var text in new[] { "Accept all", "Tümünü kabul et", "Accept" })
        {
            var button = page.Locator($"button:has-text('{text}')");
            if (await button.CountAsync() > 0)
            {
                try { await button.First.ClickAsync(new LocatorClickOptions { Timeout = 3000 }); return; }
                catch { /* not clickable — ignore */ }
            }
        }
    }

    private static async Task ApproveConsentIfPresentAsync(IPage page, Task codeTask)
    {
        // After login the browser navigates (login POST -> consent/redirect). Everything here is
        // best-effort and must never throw on a navigation race: if the app is already authorized the
        // redirect fires on its own and the code arrives without any consent click.
        for (var attempt = 0; attempt < 30 && !codeTask.IsCompleted; attempt++)
        {
            try
            {
                foreach (var text in new[] { "Allow", "Authorize", "Approve", "Continue", "İzin ver", "Onayla", "Devam" })
                {
                    if (codeTask.IsCompleted) return;
                    var button = page.Locator($"button:has-text('{text}'), a:has-text('{text}'), input[value='{text}']");
                    if (await button.CountAsync() > 0)
                    {
                        await button.First.ClickAsync(new LocatorClickOptions { Timeout = 2000 });
                        return;
                    }
                }
            }
            catch
            {
                // navigation in flight / stale locator — retry
            }
            await Task.Delay(500);
        }
    }

    private static async Task Shot(IPage page, string? dir, string cid, string step)
    {
        if (dir is null) return;
        try { await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, $"{cid}-{step}.png"), FullPage = true }); }
        catch { /* best effort */ }
    }

    public sealed record OnboardedAccount(long CtidTraderAccountId, long TraderLogin, bool IsLive);
    public sealed record OnboardedCid(string Cid, string RefreshToken, string AccessToken, bool IsLive,
        IReadOnlyList<OnboardedAccount> Accounts);
}

// Minimal HTTPS listener at https://localhost:7080/openapi/callback (the app's registered redirect URI).
// Captures the OAuth `code` from cTrader's browser redirect. Self-signed cert (Playwright ignores TLS).
internal sealed class CallbackServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private CallbackServer(WebApplication app) => _app = app;

    public static async Task<CallbackServer> StartAsync(TaskCompletionSource<string> codeSource)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
            options.ListenLocalhost(7080, listen => listen.UseHttps(SelfSignedCertificate())));

        var app = builder.Build();
        app.MapGet("/openapi/callback", (HttpContext ctx) =>
        {
            var code = ctx.Request.Query["code"].ToString();
            if (!string.IsNullOrEmpty(code)) codeSource.TrySetResult(code);
            return Results.Content("<html><body>You can close this window.</body></html>", "text/html");
        });

        await app.StartAsync();
        return new CallbackServer(app);
    }

    private static X509Certificate2 SelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

internal sealed class OnboardConnectionFactory : IOpenApiConnectionFactory
{
    private readonly IOpenApiTransportFactory _transport = new TcpSslOpenApiTransportFactory();

    public OpenApiConnection Create(bool live, string clientId, string clientSecret) => new(
        _transport,
        live ? "live.ctraderapi.com" : "demo.ctraderapi.com",
        5035, clientId, clientSecret,
        new OpenApiConnectionOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(20),
            InboundWatchdogTimeout = TimeSpan.FromSeconds(30)
        },
        NullLogger<OpenApiConnection>.Instance);
}
