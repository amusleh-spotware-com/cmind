using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace CTraderOpenApi.Transport;

public sealed class TcpSslOpenApiTransport(string host, int port) : IOpenApiTransport
{
    private const int LengthPrefixBytes = 4;
    private const int MaxFrameBytes = 16 * 1024 * 1024;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private TcpClient? _tcp;
    private SslStream? _ssl;

    public async ValueTask ConnectAsync(CancellationToken ct)
    {
        var tcp = new TcpClient { NoDelay = true };
        await tcp.ConnectAsync(host, port, ct);
        var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
        _tcp = tcp;
        _ssl = ssl;
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var ssl = _ssl ?? throw new InvalidOperationException("Transport not connected.");
        if (payload.Length > MaxFrameBytes) throw new InvalidOperationException("Outbound frame exceeds maximum size.");

        var prefix = new byte[LengthPrefixBytes];
        BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);

        await _sendLock.WaitAsync(ct);
        try
        {
            await ssl.WriteAsync(prefix, ct);
            await ssl.WriteAsync(payload, ct);
            await ssl.FlushAsync(ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async IAsyncEnumerable<byte[]> ReceiveAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var ssl = _ssl ?? throw new InvalidOperationException("Transport not connected.");
        var prefix = new byte[LengthPrefixBytes];

        while (!ct.IsCancellationRequested)
        {
            await ssl.ReadExactlyAsync(prefix, ct);
            var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
            if (length <= 0 || length > MaxFrameBytes)
                throw new InvalidOperationException($"Inbound frame length {length} is invalid.");

            var body = new byte[length];
            await ssl.ReadExactlyAsync(body, ct);
            yield return body;
        }
    }

    public async ValueTask DisconnectAsync()
    {
        var ssl = _ssl;
        _ssl = null;
        if (ssl is not null)
        {
            try
            {
                await ssl.DisposeAsync();
            }
            catch (IOException)
            {
            }
        }

        _tcp?.Dispose();
        _tcp = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _sendLock.Dispose();
    }
}

public sealed class TcpSslOpenApiTransportFactory : IOpenApiTransportFactory
{
    public IOpenApiTransport Create(string host, int port) => new TcpSslOpenApiTransport(host, port);
}
