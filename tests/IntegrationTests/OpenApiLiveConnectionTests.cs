using System.Text.Json;
using CTraderOpenApi;
using CTraderOpenApi.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

// Live smoke test against the real cTrader Open API demo endpoint. Skips automatically unless
// secrets/openapi-test-app.local.json (gitignored) is present. Validates the resilient client's
// TCP-SSL transport, protobuf framing and application-authentication handshake end to end.
public sealed class OpenApiLiveConnectionTests
{
    private const string DemoHost = "demo.ctraderapi.com";
    private const int Port = 5035;

    [Fact]
    public async Task Connects_and_application_authenticates_against_demo()
    {
        var credentials = LoadCredentials();
        if (credentials is null) return;

        var options = new OpenApiConnectionOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(15),
            InboundWatchdogTimeout = TimeSpan.FromSeconds(30)
        };

        await using var connection = new OpenApiConnection(
            new TcpSslOpenApiTransportFactory(), DemoHost, Port,
            credentials.ClientId, credentials.ClientSecret, options, NullLogger<OpenApiConnection>.Instance,
            TimeProvider.System);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await connection.StartAsync(cts.Token);

        connection.State.Should().Be(ConnectionState.Connected);
    }

    // Single source of truth: secrets/dev-credentials.local.json (OpenApi.App).
    private static Credentials? LoadCredentials()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "secrets", "dev-credentials.local.json");
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("OpenApi", out var openApi)
                    && openApi.TryGetProperty("App", out var app)
                    && app.TryGetProperty("ClientId", out var clientId)
                    && !string.IsNullOrWhiteSpace(clientId.GetString()))
                    return new Credentials(clientId.GetString()!, app.GetProperty("ClientSecret").GetString()!);
                return null;
            }
            directory = directory.Parent;
        }

        return null;
    }

    private sealed record Credentials(string ClientId, string ClientSecret);
}
