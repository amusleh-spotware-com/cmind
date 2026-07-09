using CTraderOpenApi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OpenApiConnectionTests
{
    private static OpenApiConnectionOptions FastOptions => new()
    {
        HeartbeatInterval = TimeSpan.FromSeconds(30),
        RequestTimeout = TimeSpan.FromSeconds(2),
        InboundWatchdogTimeout = TimeSpan.FromSeconds(30),
        BackoffInitial = TimeSpan.FromMilliseconds(10),
        BackoffMax = TimeSpan.FromMilliseconds(50)
    };

    private static OpenApiConnection Create(FakeOpenApiTransportFactory factory)
        => new(factory, "host", 5035, "client", "secret", FastOptions, NullLogger<OpenApiConnection>.Instance);

    [Fact]
    public async Task StartAsync_reaches_connected_after_app_auth()
    {
        var factory = new FakeOpenApiTransportFactory(FakeResponders.Handshake);
        await using var connection = Create(factory);

        await connection.StartAsync(CancellationToken.None);

        connection.State.Should().Be(ConnectionState.Connected);
        factory.Created.Should().ContainSingle();
    }

    [Fact]
    public async Task Fatal_app_auth_error_faults_connection()
    {
        var factory = new FakeOpenApiTransportFactory(FakeResponders.HandshakeThenFail);
        await using var connection = Create(factory);

        var act = () => connection.StartAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<OpenApiException>()).Which.Error.Kind.Should().Be(OpenApiErrorKind.Fatal);
        connection.State.Should().Be(ConnectionState.Faulted);
    }

    [Fact]
    public async Task Dropped_connection_reconnects_and_raises_reconnected()
    {
        var factory = new FakeOpenApiTransportFactory(FakeResponders.Handshake);
        await using var connection = Create(factory);

        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.OnReconnected = _ =>
        {
            reconnected.TrySetResult();
            return Task.CompletedTask;
        };

        await connection.StartAsync(CancellationToken.None);
        factory.Created[0].Drop();

        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        factory.Created.Count.Should().BeGreaterThan(1);
        connection.State.Should().Be(ConnectionState.Connected);
    }
}
