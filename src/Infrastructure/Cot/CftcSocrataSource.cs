using System.Globalization;
using System.Text.Json;
using Core.Cot;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Cot;

/// <summary>
/// The CFTC Commitment of Traders connector over the public Socrata datasets (one dataset per report
/// variant). Fetches the weekly rows since a date, optionally bounded to a set of contract codes, and maps
/// each raw row to a <see cref="CotSourceReport"/> with its kind-specific category breakdown. Keyless (an
/// optional app token only raises the rate limit). Lives behind a resilient, rate-limited typed
/// <c>HttpClient</c>; ingest-side only, never on a read path.
/// </summary>
public sealed class CftcSocrataSource(HttpClient httpClient, IOptionsMonitor<AppOptions> options) : ICotSource
{
    public string Name => "CFTC";

    /// <summary>The Socrata dataset resource id for each of the six report variants.</summary>
    public static string DatasetId(CotReportKind kind, bool combined) => (kind, combined) switch
    {
        (CotReportKind.Legacy, false) => "6dca-aqww",
        (CotReportKind.Legacy, true) => "jun7-fc8e",
        (CotReportKind.Disaggregated, false) => "72hh-3qpy",
        (CotReportKind.Disaggregated, true) => "kh3c-gbw2",
        (CotReportKind.Tff, false) => "gpe5-46if",
        (CotReportKind.Tff, true) => "yw9f-hn96",
        _ => "6dca-aqww"
    };

    private sealed record ColumnMap(CotTraderCategory Category, string LongCol, string ShortCol, string? SpreadCol);

    private static readonly ColumnMap[] Legacy =
    [
        new(CotTraderCategory.NonCommercial, "noncomm_positions_long_all", "noncomm_positions_short_all", "noncomm_postions_spread_all"),
        new(CotTraderCategory.Commercial, "comm_positions_long_all", "comm_positions_short_all", null),
        new(CotTraderCategory.NonReportable, "nonrept_positions_long_all", "nonrept_positions_short_all", null)
    ];

    private static readonly ColumnMap[] Disaggregated =
    [
        new(CotTraderCategory.ProducerMerchant, "prod_merc_positions_long", "prod_merc_positions_short", null),
        new(CotTraderCategory.SwapDealer, "swap_positions_long_all", "swap__positions_short_all", "swap__positions_spread_all"),
        new(CotTraderCategory.ManagedMoney, "m_money_positions_long_all", "m_money_positions_short_all", "m_money_positions_spread"),
        new(CotTraderCategory.OtherReportable, "other_rept_positions_long", "other_rept_positions_short", "other_rept_positions_spread"),
        new(CotTraderCategory.NonReportable, "nonrept_positions_long_all", "nonrept_positions_short_all", null)
    ];

    private static readonly ColumnMap[] Tff =
    [
        new(CotTraderCategory.Dealer, "dealer_positions_long_all", "dealer_positions_short_all", "dealer_positions_spread_all"),
        new(CotTraderCategory.AssetManager, "asset_mgr_positions_long", "asset_mgr_positions_short", "asset_mgr_positions_spread"),
        new(CotTraderCategory.LeveragedFunds, "lev_money_positions_long", "lev_money_positions_short", "lev_money_positions_spread"),
        new(CotTraderCategory.OtherReportable, "other_rept_positions_long", "other_rept_positions_short", "other_rept_positions_spread"),
        new(CotTraderCategory.NonReportable, "nonrept_positions_long_all", "nonrept_positions_short_all", null)
    ];

    private static ColumnMap[] MapFor(CotReportKind kind) => kind switch
    {
        CotReportKind.Legacy => Legacy,
        CotReportKind.Disaggregated => Disaggregated,
        CotReportKind.Tff => Tff,
        _ => Legacy
    };

    public async Task<IReadOnlyList<CotSourceReport>> FetchAsync(
        CotReportKind kind, bool combined, DateTimeOffset since,
        IReadOnlyCollection<string>? contractCodes, CancellationToken ct)
    {
        var where = $"report_date_as_yyyy_mm_dd >= '{since.UtcDateTime:yyyy-MM-dd}T00:00:00.000'";
        if (contractCodes is { Count: > 0 })
        {
            var list = string.Join(",", contractCodes.Select(c => $"'{c.Replace("'", "''", StringComparison.Ordinal)}'"));
            where += $" AND cftc_contract_market_code in({list})";
        }

        var url = $"resource/{DatasetId(kind, combined)}.json"
                  + $"?$where={Uri.EscapeDataString(where)}"
                  + "&$limit=50000&$order=report_date_as_yyyy_mm_dd";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var appToken = options.CurrentValue.Cot.SocrataAppToken;
        if (!string.IsNullOrWhiteSpace(appToken)) request.Headers.Add("X-App-Token", appToken);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return Parse(stream, kind, combined);
    }

    /// <summary>Parses a Socrata JSON array for the given report variant into source reports.</summary>
    public static IReadOnlyList<CotSourceReport> Parse(Stream json, CotReportKind kind, bool combined)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];

        var map = MapFor(kind);
        var reports = new List<CotSourceReport>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            var code = GetString(row, "cftc_contract_market_code")?.Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (!TryParseReportDate(GetString(row, "report_date_as_yyyy_mm_dd"), out var reportDate)) continue;

            var openInterest = GetLong(row, "open_interest_all") ?? 0;
            var oiChange = GetLong(row, "change_in_open_interest_all");

            var (name, exchange) = SplitMarket(
                GetString(row, "contract_market_name"), GetString(row, "market_and_exchange_names"));

            var categories = new List<CotSourceCategory>(map.Length);
            foreach (var column in map)
            {
                var longs = GetLong(row, column.LongCol);
                var shorts = GetLong(row, column.ShortCol);
                if (longs is null && shorts is null) continue;
                var spread = column.SpreadCol is null ? 0 : GetLong(row, column.SpreadCol) ?? 0;
                categories.Add(new CotSourceCategory(
                    column.Category, longs ?? 0, shorts ?? 0, spread, null, null));
            }

            if (categories.Count == 0) continue;

            reports.Add(new CotSourceReport(
                code, name, exchange, kind, combined, reportDate, openInterest, oiChange, categories));
        }

        return reports;
    }

    private static (string Name, string Exchange) SplitMarket(string? contractName, string? marketAndExchange)
    {
        var exchange = string.Empty;
        if (!string.IsNullOrWhiteSpace(marketAndExchange))
        {
            var idx = marketAndExchange.IndexOf(" - ", StringComparison.Ordinal);
            if (idx >= 0) exchange = marketAndExchange[(idx + 3)..].Trim();
        }

        var name = !string.IsNullOrWhiteSpace(contractName)
            ? contractName.Trim()
            : marketAndExchange?.Trim() ?? string.Empty;
        return (name, exchange);
    }

    private static string? GetString(JsonElement row, string name)
        => row.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static long? GetLong(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var element)) return null;
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(
                element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) => s,
            _ => null
        };
    }

    private static bool TryParseReportDate(string? value, out DateTimeOffset instant)
    {
        instant = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!DateTime.TryParse(
                value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return false;
        instant = new DateTimeOffset(parsed.Date, TimeSpan.Zero);
        return true;
    }
}
