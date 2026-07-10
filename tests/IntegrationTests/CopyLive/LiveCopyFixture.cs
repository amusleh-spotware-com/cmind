using CTraderOpenApi;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using CTraderOpenApi.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests.CopyLive;

// Boots the shared state for the live copy-trading tests against real cTrader accounts. Reads the
// app credentials + the multi-cID token cache from the gitignored secrets, rotates each cID's access
// token via its refresh token (no browser), and exposes a flat pool of usable accounts.
//
// SAFETY: only DEMO accounts (IsLive == false) are ever exposed for trading, so no test can place an
// order on a live/funded account. Live accounts in the cache are ignored.
public sealed class LiveCopyFixture : IAsyncLifetime
{
    public bool Available { get; private set; }
    public string SkipReason { get; private set; } = "";
    public string ClientId { get; private set; } = "";
    public string ClientSecret { get; private set; } = "";
    public IReadOnlyList<LiveAccount> DemoAccounts { get; private set; } = [];
    public IOpenApiConnectionFactory ConnectionFactory { get; } =
        new LiveConnectionFactory(new TcpSslOpenApiTransportFactory());

    public sealed record LiveAccount(string Cid, long Ctid, long Login, string AccessToken);

    public async Task InitializeAsync()
    {
        var app = LiveCopySecrets.LoadApp();
        var tokens = LiveCopySecrets.LoadTokens();
        if (app is null || tokens is null || tokens.Cids.Count == 0)
        {
            SkipReason = $"Live copy secrets missing (need secrets/{LiveCopySecrets.AppFileName} and " +
                         $"secrets/{LiveCopySecrets.TokensFileName}). Run the OAuth onboarding once " +
                         "(see docs/testing/live-copy-trading.md).";
            return;
        }

        ClientId = app.ClientId;
        ClientSecret = app.ClientSecret;

        using var http = new HttpClient { BaseAddress = new Uri(Core.Constants.OpenApiEndpoints.AuthBaseUrl) };
        var client = new OpenApiTokenClient(http);

        var refreshedCids = new List<LiveCopySecrets.CidTokens>();
        var pool = new List<LiveAccount>();
        foreach (var cid in tokens.Cids)
        {
            var refreshed = await client.RefreshAsync(ClientId, ClientSecret, cid.RefreshToken, CancellationToken.None);
            var access = refreshed.AccessToken;
            var newRefresh = string.IsNullOrEmpty(refreshed.RefreshToken) ? cid.RefreshToken : refreshed.RefreshToken;
            refreshedCids.Add(cid with { AccessToken = access, RefreshToken = newRefresh });

            foreach (var account in cid.Accounts.Where(a => !a.IsLive)) // DEMO ONLY
                pool.Add(new LiveAccount(cid.Cid, account.CtidTraderAccountId, account.TraderLogin, access));
        }

        LiveCopySecrets.SaveTokens(new LiveCopySecrets.TokenCache(refreshedCids));
        DemoAccounts = pool;
        Available = pool.Count >= 2;
        if (!Available)
            SkipReason = $"Need at least two demo accounts across the authorized cIDs (found {pool.Count}).";
    }

    public OpenApiTradingSession NewSession(params LiveAccount[] accounts)
    {
        // All demo accounts live on the demo gateway, so a single demo connection can hold them all,
        // even across different cIDs (each account authenticates with its own access token).
        var session = new OpenApiTradingSession(ConnectionFactory.Create(live: false, ClientId, ClientSecret));
        foreach (var account in accounts) session.AttachAccount(account.Ctid, account.AccessToken);
        return session;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

// Minimal connection factory for the live tests — real TCP-SSL transport to the demo gateway.
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
        NullLogger<OpenApiConnection>.Instance,
        TimeProvider.System);
}

[CollectionDefinition(Name)]
public sealed class LiveCopyCollection : ICollectionFixture<LiveCopyFixture>
{
    public const string Name = "live-copy";
}
