using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Domain;

namespace Web.Json;

// A single source→destination symbol translation row with an optional per-symbol volume multiplier,
// as edited in the UI and carried in the copy-profile settings file / symbol-map CSV.
public sealed record SymbolMapRow(string Source, string Destination, double VolumeMultiplier = 1);

// The full set of per-destination copy settings a user tunes on the create page / modify dialog. It is
// the shape exported to and imported from a copy-profile settings file so a user can reuse a tuning
// across profiles without re-typing every field.
public sealed record CopyProfileSettingsModel
{
    public MoneyManagementMode Mode { get; init; } = MoneyManagementMode.LotMultiplier;
    public double Parameter { get; init; } = 1;
    public double SlippagePips { get; init; }
    public CopyDirectionFilter Direction { get; init; } = CopyDirectionFilter.Both;
    public double MinLot { get; init; }
    public double MaxLot { get; init; }
    public int MaxDelaySeconds { get; init; }
    public double MaxDrawdownPercent { get; init; }
    public double DailyLossLimit { get; init; }
    public bool Reverse { get; init; }
    public bool ForceMinLot { get; init; }
    public bool CopyStopLoss { get; init; } = true;
    public bool CopyTakeProfit { get; init; } = true;
    public bool MirrorPartialClose { get; init; } = true;
    public bool MirrorScaleIn { get; init; }
    public bool CopyPendingOrders { get; init; }
    public bool CopyTrailingStop { get; init; }
    public bool CopyPendingExpiry { get; init; } = true;
    public bool CopyMasterSlippage { get; init; } = true;
    public CopyOrderTypes OrderTypes { get; init; } = CopyOrderTypes.All;
    public SymbolFilterMode SymbolFilterMode { get; init; } = SymbolFilterMode.None;
    public IReadOnlyList<string> SymbolFilters { get; init; } = [];
    public IReadOnlyList<SymbolMapRow> SymbolMap { get; init; } = [];
}

// Import/export of copy-profile settings (as a JSON file) and of the symbol map (as a CSV file). Pure,
// culture-invariant conversions so a file exported on one machine imports identically on another; the UI
// only downloads/uploads the resulting text. Import is defensive — malformed input yields null / an empty
// list rather than throwing at the user.
public static class CopyProfilePortability
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ExportSettingsJson(CopyProfileSettingsModel settings)
        => JsonSerializer.Serialize(settings, _json);

    public static CopyProfileSettingsModel? ImportSettingsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<CopyProfileSettingsModel>(json, _json); }
        catch (JsonException) { return null; }
    }

    // Reuse the domain's canonical CSV formatter so an exported file is byte-identical to the one the
    // per-destination endpoint emits (Source,Destination,VolumeMultiplier, invariant culture).
    public static string SymbolMapToCsv(IReadOnlyList<SymbolMapRow> rows)
        => CopySymbolMapCsv.Format(rows.Select(r => (r.Source, r.Destination, r.VolumeMultiplier)));

    // Lenient client-side parse for the in-form editor: skip blank/malformed rows instead of throwing, so
    // one bad line in a spreadsheet doesn't reject the whole import. The domain re-validates strictly on save.
    public static IReadOnlyList<SymbolMapRow> ParseSymbolMapCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        var rows = new List<SymbolMapRow>();
        foreach (var raw in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cells = raw.TrimEnd('\r').Split(',', StringSplitOptions.TrimEntries);
            if (cells.Length < 2) continue;
            var source = cells[0];
            var destination = cells[1];
            if (source.Length == 0 || destination.Length == 0) continue;
            if (string.Equals(source, "Source", StringComparison.OrdinalIgnoreCase)
                && string.Equals(destination, "Destination", StringComparison.OrdinalIgnoreCase))
                continue;
            var multiplier = 1d;
            if (cells.Length >= 3
                && double.TryParse(cells[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var m)
                && m > 0)
                multiplier = m;
            rows.Add(new SymbolMapRow(source, destination, multiplier));
        }
        return rows;
    }
}
