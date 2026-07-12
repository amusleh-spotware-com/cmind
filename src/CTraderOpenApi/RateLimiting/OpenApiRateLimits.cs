using CTraderOpenApi.Messages;

namespace CTraderOpenApi.RateLimiting;

/// <summary>
/// cTrader Open API message-type rate categories. The server enforces different per-connection limits per
/// message class (a general messages/sec cap, a stricter historical-data cap); the client paces each
/// category independently so a burst never trips a server-side block.
/// </summary>
public enum OpenApiRateCategory
{
    /// <summary>Default for trading and read messages — the general per-connection cap.</summary>
    General = 0,

    /// <summary>Historical-data requests (trendbars, tick data) — cTrader throttles these harder.</summary>
    HistoricalData = 1,

    /// <summary>Keep-alive and handshake (heartbeat, app/account auth) — never paced.</summary>
    Exempt = 2
}

/// <summary>
/// The single source of truth mapping a cTrader payload type to its <see cref="OpenApiRateCategory"/>,
/// exactly per the Open API docs. Config, buckets and tests all reference this — no scattered payload lists.
/// </summary>
public static class OpenApiRateLimits
{
    public static OpenApiRateCategory Classify(uint payloadType) => payloadType switch
    {
        (uint)ProtoOAPayloadType.ProtoOaGetTrendbarsReq => OpenApiRateCategory.HistoricalData,
        (uint)ProtoOAPayloadType.ProtoOaGetTickdataReq => OpenApiRateCategory.HistoricalData,
        (uint)ProtoOAPayloadType.ProtoOaApplicationAuthReq => OpenApiRateCategory.Exempt,
        (uint)ProtoOAPayloadType.ProtoOaAccountAuthReq => OpenApiRateCategory.Exempt,
        (uint)ProtoPayloadType.HeartbeatEvent => OpenApiRateCategory.Exempt,
        _ => OpenApiRateCategory.General
    };
}
