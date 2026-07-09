using CTraderOpenApi;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using CTraderOpenApi.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests.CopyLive;

// Boots the shared state for the live copy-trading tests against real cTrader demo accounts.
// Reads app credentials + the cached refresh token from the gitignored secrets files, rotates the
// access token via the refresh token (no browser), and exposes a connection factory + account list.
// If secrets are absent the fixture is "unavailable" and every live test skips.
public sealed class LiveCopyFixture : IAsyncLifetime
{
    public bool Available { get; private set; }
    public string SkipReason { get; private set; } = "";
    public string ClientId { get; private set; } = "";
    public string ClientSecret { get; private set; } = "";
    public string AccessToken { get; private set; } = "";
    public bool IsLive { get; private set; }
    public IReadOnlyList<LiveCopySecrets.CachedAccount> Accounts { get; private set; } = [];
    public IOpenApiConnectionFactory ConnectionFactory { get; } =
        new LiveConnectionFactory(new TcpSslOpenApiTransportFactory());

    public async Task InitializeAsync()
    {
        var app = LiveCopySecrets.LoadApp();
        var tokens = LiveCopySecrets.LoadTokens();
        if (app is null || tokens is null)
        {
            SkipReason = $"Live copy secrets missing (need secrets/{LiveCopySecrets.AppFileName} and " +
                         $"secrets/{LiveCopySecrets.TokensFileName}). See docs/testing/live-copy-trading.md.";
            return;
        }

        ClientId = app.ClientId;
        ClientSecret = app.ClientSecret;
        IsLive = tokens.IsLive;
        Accounts = tokens.Accounts;

        using var http = new HttpClient { BaseAddress = new Uri(Core.Constants.OpenApiEndpoints.AuthBaseUrl) };
        var client = new OpenApiTokenClient(http);
        var refreshed = await client.RefreshAsync(ClientId, ClientSecret, tokens.RefreshToken, CancellationToken.None);

        AccessToken = refreshed.AccessToken;
        var newRefresh = string.IsNullOrEmpty(refreshed.RefreshToken) ? tokens.RefreshToken : refreshed.RefreshToken;
        LiveCopySecrets.SaveTokens(tokens with { AccessToken = AccessToken, RefreshToken = newRefresh });
        Available = true;
    }

    public OpenApiTradingSession NewSession(params long[] ctidTraderAccountIds)
    {
        var session = new OpenApiTradingSession(ConnectionFactory.Create(IsLive, ClientId, ClientSecret));
        foreach (var ctid in ctidTraderAccountIds) session.AttachAccount(ctid, AccessToken);
        return session;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

// Minimal connection factory for the live tests — real TCP-SSL transport to the demo/live gateway.
internal sealed class LiveConnectionFactory(IOpenApiTransportFactory transportFactory) : IOpenApiConnectionFactory
{
    private const string DemoHost = "demo.ctraderapi.com";
    private const string LiveHost = "live.ctraderapi.com";
    private const int Port = 5035;

    public OpenApiConnection Create(bool live, string clientId, string clientSecret) => new(
        transportFactory,
        live ? LiveHost : DemoHost,
        Port,
        clientId,
        clientSecret,
        new OpenApiConnectionOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(20),
            InboundWatchdogTimeout = TimeSpan.FromSeconds(30),
            BackoffInitial = TimeSpan.FromMilliseconds(200),
            BackoffMax = TimeSpan.FromSeconds(5)
        },
        NullLogger<OpenApiConnection>.Instance);
}

[CollectionDefinition(Name)]
public sealed class LiveCopyCollection : ICollectionFixture<LiveCopyFixture>
{
    public const string Name = "live-copy";
}
