namespace CTraderOpenApi.Transport;

public interface IOpenApiTransport : IAsyncDisposable
{
    ValueTask ConnectAsync(CancellationToken ct);

    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);

    IAsyncEnumerable<byte[]> ReceiveAsync(CancellationToken ct);

    ValueTask DisconnectAsync();
}

public interface IOpenApiTransportFactory
{
    IOpenApiTransport Create(string host, int port);
}
