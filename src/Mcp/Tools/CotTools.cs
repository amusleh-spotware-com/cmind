using System.ComponentModel;
using System.Globalization;
using Core;
using Core.Cot;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

/// <summary>
/// The Commitment of Traders feed as MCP tools — full parity with the REST read API so every AI client and
/// in-app AI feature gets the same positioning data a cBot has. Read-only, point-in-time correct (pass
/// <c>asOf</c> to see the data as it was public at a past instant, no look-ahead), and served through the
/// same <see cref="ICotReports"/> domain read side as the REST API.
/// </summary>
[McpServerToolType]
public sealed class CotTools(ICotReports cot, TimeProvider timeProvider)
{
    [McpServerTool, Description(
        "Catalog of tracked CFTC contract markets (contract code, name, exchange, asset group, mapped symbol). " +
        "Filter by group (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) or a keyword.")]
    public async Task<object> CotMarkets(
        [Description("Asset group filter (optional)")] string? group = null,
        [Description("Keyword search over name/code (optional)")] string? q = null)
        => await cot.GetMarketsAsync(Group(group), string.IsNullOrWhiteSpace(q) ? null : q, CancellationToken.None);

    [McpServerTool, Description(
        "The latest weekly COT snapshot for a contract market: the long/short/net breakdown per trader " +
        "category, open interest and the COT index (0-100, where the speculator net sits in its range). " +
        "Pass asOf (ISO-8601) for a point-in-time view with no look-ahead.")]
    public async Task<object?> CotLatest(
        [Description("CFTC contract market code, e.g. 099741 for Euro FX")] string code,
        [Description("Report kind: Legacy, Disaggregated or Tff (default Legacy)")] string? kind = null,
        [Description("Futures+options combined when true; futures-only when false (default false)")] bool combined = false,
        [Description("Point-in-time anchor, ISO-8601 (optional)")] string? asOf = null)
        => await cot.GetLatestAsync(new ContractMarketCode(code), Kind(kind), combined, Instant(asOf), CancellationToken.None);

    [McpServerTool, Description(
        "Weekly COT history for a contract market over a window — each point carries the category breakdown, " +
        "speculator net and COT index, for reasoning or backtesting a positioning signal. Point-in-time via asOf.")]
    public async Task<object> CotHistory(
        [Description("CFTC contract market code, e.g. 088691 for Gold")] string code,
        [Description("Report kind: Legacy, Disaggregated or Tff (default Legacy)")] string? kind = null,
        [Description("Futures+options combined when true; futures-only when false (default false)")] bool combined = false,
        [Description("Window start, ISO-8601 (optional; default 3 years back)")] string? from = null,
        [Description("Window end, ISO-8601 (optional; default now)")] string? to = null,
        [Description("Point-in-time anchor, ISO-8601 (optional)")] string? asOf = null)
    {
        var now = timeProvider.GetUtcNow();
        return await cot.GetHistoryAsync(
            new ContractMarketCode(code), Kind(kind), combined,
            Instant(from) ?? now.AddYears(-3), Instant(to) ?? now, Instant(asOf), CancellationToken.None);
    }

    [McpServerTool, Description("Per-source freshness and coverage so an agent can judge how far to trust the COT data.")]
    public async Task<object> CotHealth()
        => await cot.GetHealthAsync(CancellationToken.None);

    private static CotReportKind Kind(string? value) =>
        Enum.TryParse<CotReportKind>(value, ignoreCase: true, out var kind) ? kind : CotReportKind.Legacy;

    private static CotContractGroup? Group(string? value) =>
        Enum.TryParse<CotContractGroup>(value, ignoreCase: true, out var group) ? group : null;

    private static DateTimeOffset? Instant(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var instant)
            ? instant
            : null;
}
