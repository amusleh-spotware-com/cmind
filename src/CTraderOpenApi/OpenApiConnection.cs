using System.Collections.Concurrent;
using System.Threading.Channels;
using CTraderOpenApi.Logging;
using CTraderOpenApi.Messages;
using CTraderOpenApi.RateLimiting;
using CTraderOpenApi.Transport;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace CTraderOpenApi;

public sealed class OpenApiConnection : IAsyncDisposable
{
    private readonly IOpenApiTransportFactory _transportFactory;
    private readonly string _host;
    private readonly int _port;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly OpenApiConnectionOptions _options;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly BackoffPolicy _backoff;
    private readonly OpenApiRateGate _rateGate;

    private readonly Channel<ProtoMessage> _outbound =
        Channel.CreateUnbounded<ProtoMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<ProtoMessage> _events =
        Channel.CreateUnbounded<ProtoMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoMessage>> _pending = new();
    private readonly ConcurrentDictionary<long, string> _accounts = new();

    private CancellationTokenSource? _lifetimeCts;
    private Task? _runLoop;
    private IOpenApiTransport? _transport;
    private TaskCompletionSource _readyOnce = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _lastInboundTicks;
    private volatile ConnectionState _state = ConnectionState.Disconnected;

    public OpenApiConnection(
        IOpenApiTransportFactory transportFactory,
        string host,
        int port,
        string clientId,
        string clientSecret,
        OpenApiConnectionOptions options,
        ILogger logger,
        TimeProvider timeProvider)
    {
        _transportFactory = transportFactory;
        _host = host;
        _port = port;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
        _backoff = new BackoffPolicy(options.BackoffInitial, options.BackoffMax, options.BackoffFactor);
        _rateGate = new OpenApiRateGate(options.RateLimits, timeProvider);
    }

    public ConnectionState State => _state;

    public IAsyncEnumerable<ProtoMessage> Events => _events.Reader.ReadAllAsync();

    public Func<CancellationToken, Task>? OnReconnected { get; set; }

    public void AttachAccount(long ctidTraderAccountId, string accessToken)
        => _accounts[ctidTraderAccountId] = accessToken;

    public async Task StartAsync(CancellationToken ct)
    {
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _runLoop = Task.Run(() => RunAsync(_lifetimeCts.Token), CancellationToken.None);
        await _readyOnce.Task.WaitAsync(ct);
    }

    public async Task<ProtoMessage> SendAsync(IMessage payload, int payloadType, CancellationToken ct)
    {
        if (_state != ConnectionState.Connected)
            throw new OpenApiException(new OpenApiError("NOT_CONNECTED", "Connection not ready", OpenApiErrorKind.Recoverable, null));

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ProtoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            EnqueueOrThrow(Wrap(payload, payloadType, id));
            using var timeout = new CancellationTokenSource(_options.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            await using var reg = linked.Token.Register(static s =>
                ((TaskCompletionSource<ProtoMessage>)s!).TrySetCanceled(), tcs);
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public async Task AuthorizeAccountAsync(long ctidTraderAccountId, string accessToken, CancellationToken ct)
    {
        _accounts[ctidTraderAccountId] = accessToken;
        if (_state == ConnectionState.Connected)
            await AccountAuthAsync(ctidTraderAccountId, accessToken, ct);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                await ConnectAndServeAsync(attemptCts);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (OpenApiException oaex) when (oaex.Error.Kind == OpenApiErrorKind.Fatal)
            {
                _state = ConnectionState.Faulted;
                _logger.FatalError(oaex.Error.Code, oaex.Error.Description);
                _readyOnce.TrySetException(oaex);
                break;
            }
            catch (OpenApiException oaex) when (oaex.Error.Kind == OpenApiErrorKind.Maintenance)
            {
                _logger.MaintenanceWait(oaex.Error.MaintenanceEndsAt);
                await SafeDelay(MaintenanceDelay(oaex.Error.MaintenanceEndsAt), ct);
            }
            catch (Exception ex)
            {
                _logger.ConnectionDropped(ex.Message);
                await SafeDelay(_backoff.NextDelay(), ct);
            }
            finally
            {
                FailPending("Connection lost");
                attemptCts.Cancel();
                if (_transport is not null)
                {
                    await _transport.DisposeAsync();
                    _transport = null;
                }

                if (_state == ConnectionState.Connected)
                    _state = ConnectionState.Disconnected;
            }
        }
    }

    private async Task ConnectAndServeAsync(CancellationTokenSource attemptCts)
    {
        var ct = attemptCts.Token;
        _state = ConnectionState.Connecting;
        DrainOutbound();
        _rateGate.Reset();

        var transport = _transportFactory.Create(_host, _port);
        _transport = transport;
        await transport.ConnectAsync(ct);
        Volatile.Write(ref _lastInboundTicks, _timeProvider.GetUtcNow().UtcTicks);

        var receive = Task.Run(() => ReceivePumpAsync(transport, ct), CancellationToken.None);
        var send = Task.Run(() => SendPumpAsync(transport, ct), CancellationToken.None);

        await AppAuthAsync(ct);
        foreach (var (ctid, token) in _accounts.ToArray())
            await AccountAuthAsync(ctid, token, ct);

        _state = ConnectionState.Connected;
        _logger.Connected(_host, _port);
        _backoff.Reset();

        var firstConnect = _readyOnce.TrySetResult();
        if (!firstConnect && OnReconnected is not null)
            await OnReconnected(ct);

        var heartbeat = HeartbeatPumpAsync(ct);
        var watchdog = WatchdogAsync(ct);

        await Task.WhenAny(receive, send, heartbeat, watchdog);
        attemptCts.Cancel();
        await Task.WhenAll(Swallow(receive), Swallow(send), Swallow(heartbeat), Swallow(watchdog));

        foreach (var task in new[] { receive, send, heartbeat, watchdog })
            if (task.IsFaulted)
                throw task.Exception!.InnerException ?? task.Exception!;

        throw new IOException("Open API connection ended.");
    }

    private async Task AppAuthAsync(CancellationToken ct)
    {
        var req = new ProtoOAApplicationAuthReq { ClientId = _clientId, ClientSecret = _clientSecret };
        await SendHandshakeAsync(req, (int)ProtoOAPayloadType.ProtoOaApplicationAuthReq, ct);
    }

    private async Task AccountAuthAsync(long ctidTraderAccountId, string accessToken, CancellationToken ct)
    {
        var req = new ProtoOAAccountAuthReq { CtidTraderAccountId = ctidTraderAccountId, AccessToken = accessToken };
        await SendHandshakeAsync(req, (int)ProtoOAPayloadType.ProtoOaAccountAuthReq, ct);
    }

    private async Task SendHandshakeAsync(IMessage payload, int payloadType, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ProtoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            EnqueueOrThrow(Wrap(payload, payloadType, id));
            using var timeout = new CancellationTokenSource(_options.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            await using var reg = linked.Token.Register(static s =>
                ((TaskCompletionSource<ProtoMessage>)s!).TrySetCanceled(), tcs);
            await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private void EnqueueOrThrow(ProtoMessage message)
    {
        if (!_outbound.Writer.TryWrite(message))
            throw new OpenApiException(new OpenApiError(
                "NOT_CONNECTED", "Outbound channel is closed", OpenApiErrorKind.Recoverable, null));
    }

    private async Task SendPumpAsync(IOpenApiTransport transport, CancellationToken ct)
    {
        await foreach (var msg in _outbound.Reader.ReadAllAsync(ct))
        {
            await _rateGate.AcquireAsync(msg.PayloadType, ct);
            await transport.SendAsync(msg.ToByteArray(), ct);
        }
    }

    private async Task ReceivePumpAsync(IOpenApiTransport transport, CancellationToken ct)
    {
        await foreach (var bytes in transport.ReceiveAsync(ct))
        {
            Volatile.Write(ref _lastInboundTicks, _timeProvider.GetUtcNow().UtcTicks);
            var msg = ProtoMessage.Parser.ParseFrom(bytes);
            Dispatch(msg);
        }
    }

    private void Dispatch(ProtoMessage msg)
    {
        if (!string.IsNullOrEmpty(msg.ClientMsgId) && _pending.TryRemove(msg.ClientMsgId, out var tcs))
        {
            if (TryParseError(msg, out var err))
                tcs.TrySetException(new OpenApiException(err));
            else
                tcs.TrySetResult(msg);
            return;
        }

        if (TryParseError(msg, out var unsolicited)
            && unsolicited.Kind is OpenApiErrorKind.Maintenance or OpenApiErrorKind.Fatal)
            throw new OpenApiException(unsolicited);

        _events.Writer.TryWrite(msg);
    }

    private static bool TryParseError(ProtoMessage msg, out OpenApiError error)
    {
        if (msg.PayloadType == (uint)ProtoOAPayloadType.ProtoOaErrorRes)
        {
            var e = ProtoOAErrorRes.Parser.ParseFrom(msg.Payload);
            error = OpenApiError.Classify(e.ErrorCode, e.Description, ToMaintenance(e.MaintenanceEndTimestamp, seconds: true));
            return true;
        }

        if (msg.PayloadType == (uint)ProtoPayloadType.ErrorRes)
        {
            var e = ProtoErrorRes.Parser.ParseFrom(msg.Payload);
            error = OpenApiError.Classify(e.ErrorCode, e.Description, ToMaintenance((long)e.MaintenanceEndTimestamp, seconds: false));
            return true;
        }

        error = null!;
        return false;
    }

    private static DateTimeOffset? ToMaintenance(long value, bool seconds)
        => value <= 0 ? null : seconds
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : DateTimeOffset.FromUnixTimeMilliseconds(value);

    private async Task HeartbeatPumpAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_options.HeartbeatInterval, ct);
            _outbound.Writer.TryWrite(Wrap(new ProtoHeartbeatEvent(), (int)ProtoPayloadType.HeartbeatEvent, null));
        }
    }

    private async Task WatchdogAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(1000, _options.InboundWatchdogTimeout.TotalMilliseconds / 2));
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);
            var last = new DateTime(Volatile.Read(ref _lastInboundTicks), DateTimeKind.Utc);
            if (_timeProvider.GetUtcNow().UtcDateTime - last > _options.InboundWatchdogTimeout)
                throw new IOException("Inbound watchdog timeout.");
        }
    }

    private TimeSpan MaintenanceDelay(DateTimeOffset? endsAt)
    {
        if (endsAt is null)
            return _options.BackoffMax;

        var remaining = endsAt.Value - _timeProvider.GetUtcNow() + _options.MaintenanceMinDelay;
        if (remaining < _options.MaintenanceMinDelay) remaining = _options.MaintenanceMinDelay;
        if (remaining > _options.MaintenanceMaxDelay) remaining = _options.MaintenanceMaxDelay;
        return remaining;
    }

    private void FailPending(string reason)
    {
        foreach (var key in _pending.Keys)
            if (_pending.TryRemove(key, out var tcs))
                tcs.TrySetException(new OpenApiException(
                    new OpenApiError("DISCONNECTED", reason, OpenApiErrorKind.Recoverable, null)));
    }

    private void DrainOutbound()
    {
        while (_outbound.Reader.TryRead(out _))
        {
        }
    }

    private static ProtoMessage Wrap(IMessage payload, int payloadType, string? clientMsgId)
    {
        var msg = new ProtoMessage { PayloadType = (uint)payloadType, Payload = payload.ToByteString() };
        if (!string.IsNullOrEmpty(clientMsgId)) msg.ClientMsgId = clientMsgId;
        return msg;
    }

    private static async Task SafeDelay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task Swallow(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetimeCts?.Cancel();
        if (_runLoop is not null)
            await Swallow(_runLoop);
        if (_transport is not null)
            await _transport.DisposeAsync();
        _lifetimeCts?.Dispose();
        _events.Writer.TryComplete();
        _outbound.Writer.TryComplete();
    }
}
