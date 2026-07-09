using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CTraderOpenApi.Messages;
using CTraderOpenApi.Transport;
using Google.Protobuf;

namespace UnitTests.CopyTrading;

internal sealed class FakeOpenApiTransport(Func<ProtoMessage, ProtoMessage?> responder) : IOpenApiTransport
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();

    public ValueTask ConnectAsync(CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var message = ProtoMessage.Parser.ParseFrom(payload.Span);
        var response = responder(message);
        if (response is not null) _inbound.Writer.TryWrite(response.ToByteArray());
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<byte[]> ReceiveAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in _inbound.Reader.ReadAllAsync(ct))
            yield return item;
    }

    public void Drop() => _inbound.Writer.TryComplete(new IOException("simulated drop"));

    public ValueTask DisconnectAsync()
    {
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeOpenApiTransportFactory(Func<ProtoMessage, ProtoMessage?> responder) : IOpenApiTransportFactory
{
    public List<FakeOpenApiTransport> Created { get; } = [];

    public IOpenApiTransport Create(string host, int port)
    {
        var transport = new FakeOpenApiTransport(responder);
        Created.Add(transport);
        return transport;
    }
}

internal static class FakeResponders
{
    public static ProtoMessage? Handshake(ProtoMessage request) => request.PayloadType switch
    {
        (uint)ProtoOAPayloadType.ProtoOaApplicationAuthReq =>
            Respond(request, (uint)ProtoOAPayloadType.ProtoOaApplicationAuthRes, new ProtoOAApplicationAuthRes()),
        (uint)ProtoOAPayloadType.ProtoOaAccountAuthReq =>
            Respond(request, (uint)ProtoOAPayloadType.ProtoOaAccountAuthRes, AccountAuthRes(request)),
        _ => null
    };

    public static ProtoMessage? HandshakeThenFail(ProtoMessage request) => request.PayloadType switch
    {
        (uint)ProtoOAPayloadType.ProtoOaApplicationAuthReq =>
            Respond(request, (uint)ProtoOAPayloadType.ProtoOaErrorRes,
                new ProtoOAErrorRes { ErrorCode = "CH_CLIENT_AUTH_FAILURE", Description = "bad credentials" }),
        _ => null
    };

    private static ProtoOAAccountAuthRes AccountAuthRes(ProtoMessage request)
    {
        var req = ProtoOAAccountAuthReq.Parser.ParseFrom(request.Payload);
        return new ProtoOAAccountAuthRes { CtidTraderAccountId = req.CtidTraderAccountId };
    }

    private static ProtoMessage Respond(ProtoMessage request, uint payloadType, IMessage payload)
        => new() { PayloadType = payloadType, Payload = payload.ToByteString(), ClientMsgId = request.ClientMsgId };
}
