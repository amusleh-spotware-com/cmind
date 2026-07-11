using System.Globalization;
using System.Text.Json;
using Core;
using Core.Constants;

namespace Nodes;

public static class ContainerCommandHelpers
{
    private const string WorkMount = FilePaths.ContainerWorkMount;
    private const string DataDir = FilePaths.ContainerDataDir;
    private const string AlgoFile = FilePaths.CbotAlgoFile;
    private const string ParamsFile = FilePaths.ParamsCbotsetFile;
    private const string PwdFile = FilePaths.CtidPwdFile;
    private const string ReportJson = FilePaths.ReportJsonFile;
    private const string ReportHtml = FilePaths.ReportHtmlFile;
    private const string PointsKey = "points";

    public static string? GetContainerId(Instance i) => i switch
    {
        StartingRunInstance s => s.ContainerId,
        RunningRunInstance r => r.ContainerId,
        StoppingRunInstance st => st.ContainerId,
        StoppedRunInstance st => st.ContainerId,
        FailedRunInstance f => f.ContainerId,
        StartingBacktestInstance s => s.ContainerId,
        RunningBacktestInstance r => r.ContainerId,
        StoppingBacktestInstance st => st.ContainerId,
        CompletedBacktestInstance c => c.ContainerId,
        FailedBacktestInstance f => f.ContainerId,
        _ => null
    };

    /// <summary>cTrader CLI arguments as individual tokens (no shell quoting) for exec-style invocation.</summary>
    public static List<string> BuildConsoleArgsList(Instance i, string ctid, bool hasParams)
    {
        var args = new List<string>();
        var isBacktest = i is BacktestInstance;
        args.Add(isBacktest ? CliCommands.Backtest : CliCommands.Run);
        args.Add($"{WorkMount}/{AlgoFile}");
        if (hasParams) args.Add($"{WorkMount}/{ParamsFile}");
        if (!string.IsNullOrEmpty(ctid))
        {
            args.Add(CliFlags.Ctid);
            args.Add(ctid);
            args.Add(CliFlags.PwdFile);
            args.Add($"{WorkMount}/{PwdFile}");
        }
        if (i.TradingAccount is { } ta)
        {
            args.Add(CliFlags.Account);
            args.Add(ta.AccountNumber.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrEmpty(i.Symbol)) { args.Add(CliFlags.Symbol); args.Add(i.Symbol); }
        if (!string.IsNullOrEmpty(i.Timeframe)) { args.Add(CliFlags.Period); args.Add(i.Timeframe); }

        if (i is BacktestInstance b)
        {
            args.Add(CliFlags.DataDir);
            args.Add(DataDir);
            var dataMode = BacktestDefaults.DataMode;
            if (!string.IsNullOrEmpty(b.BacktestSettingsJson))
            {
                using var doc = JsonDocument.Parse(b.BacktestSettingsJson);
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var lower = p.Name.ToLowerInvariant();
                    if (lower is "datamode" or "data-mode")
                    {
                        var mode = p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString()
                            : p.Value.ToString();
                        if (!string.IsNullOrWhiteSpace(mode)) dataMode = mode!;
                        continue;
                    }
                    var name = lower switch
                    {
                        "from" => "start",
                        "to" => "end",
                        _ => p.Name
                    };
                    var val = name is "start" or "end"
                        ? FormatBacktestDate(p.Value)
                        : p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString() ?? string.Empty
                            : p.Value.ToString();
                    args.Add($"--{name}");
                    args.Add(val);
                }
            }
            args.Add(CliFlags.DataMode);
            args.Add(dataMode);
            args.Add(CliFlags.ReportJson);
            args.Add($"{WorkMount}/{ReportJson}");
            args.Add(CliFlags.Report);
            args.Add($"{WorkMount}/{ReportHtml}");
            args.Add(CliFlags.ExitOnStop);
        }
        return args;
    }

    /// <summary>cTrader CLI arguments joined into a single shell-style string (spaces quoted).</summary>
    public static string BuildConsoleArgs(Instance i, string ctid, bool hasParams) =>
        string.Join(' ', BuildConsoleArgsList(i, ctid, hasParams).Select(QuoteIfNeeded));

    private static string QuoteIfNeeded(string token) =>
        token.Contains(' ', StringComparison.Ordinal) ? $"\"{token}\"" : token;

    private static string FormatBacktestDate(JsonElement el)
    {
        var raw = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        if (!string.IsNullOrWhiteSpace(raw)
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            return dt.ToString(BacktestDefaults.DateFormat, CultureInfo.InvariantCulture);
        return raw ?? string.Empty;
    }

    public static string JsonToCbotset(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return string.Empty;
            var parameters = new Dictionary<string, string>();
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var value = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString() ?? string.Empty
                    : p.Value.ToString();
                parameters[p.Name] = value;
            }
            return parameters.Count == 0
                ? string.Empty
                : JsonSerializer.Serialize(new { Parameters = parameters });
        }
        catch { return string.Empty; }
    }

    public static (double, long, long) ParseDockerStats(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return (0, 0, 0);
        var first = line.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var parts = first.Split('|');
        if (parts.Length < 2) return (0, 0, 0);
        double.TryParse(parts[0].Trim().TrimEnd('%'), CultureInfo.InvariantCulture, out var cpu);
        var mem = parts[1].Split('/');
        return (cpu, ParseSize(mem[0]), mem.Length > 1 ? ParseSize(mem[1]) : 0);
    }

    public static (long used, long total) ParseDf(string line)
    {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return (0, 0);
        _ = long.TryParse(parts[0], out var used);
        _ = long.TryParse(parts[1], out var total);
        return (used, total);
    }

    public static long ParseSize(string s)
    {
        s = s.Trim();
        var mult = 1L;
        if (s.EndsWith("GiB", StringComparison.Ordinal)) { mult = 1024L * 1024 * 1024; s = s[..^3]; }
        else if (s.EndsWith("MiB", StringComparison.Ordinal)) { mult = 1024L * 1024; s = s[..^3]; }
        else if (s.EndsWith("KiB", StringComparison.Ordinal)) { mult = 1024L; s = s[..^3]; }
        else if (s.EndsWith('B')) { s = s[..^1]; }
        double.TryParse(s, CultureInfo.InvariantCulture, out var v);
        return (long)(v * mult);
    }

    private static readonly string[] EquityArrayKeys = ["equityHistory", "equityCurve", "history", "equity"];
    private static readonly string[] TimeKeys = ["time", "date", "timestamp", "ts"];
    private static readonly string[] ValueKeys = ["equity", "balance", "value"];

    public static List<(DateTimeOffset Timestamp, double Value)> ParseEquityCurve(string? reportJson)
    {
        var points = new List<(DateTimeOffset, double)>();
        if (string.IsNullOrWhiteSpace(reportJson)) return points;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(reportJson); }
        catch (JsonException) { return points; }
        using (doc)
        {
            var array = FindEquityArray(doc.RootElement);
            if (array is null) return points;
            foreach (var item in array.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!TryGetProperty(item, TimeKeys, out var timeProp)) continue;
                if (!TryGetProperty(item, ValueKeys, out var valueProp)) continue;
                if (!TryParseTimestamp(timeProp, out var ts)) continue;
                if (valueProp.ValueKind != JsonValueKind.Number) continue;
                points.Add((ts, valueProp.GetDouble()));
            }
        }
        return points;
    }

    private static JsonElement? FindEquityArray(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var key in EquityArrayKeys)
            if (root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                return arr;
        foreach (var key in EquityArrayKeys)
            if (root.TryGetProperty(key, out var obj) && obj.ValueKind == JsonValueKind.Object
                && obj.TryGetProperty(PointsKey, out var pts) && pts.ValueKind == JsonValueKind.Array)
                return pts;
        return null;
    }

    private static bool TryGetProperty(JsonElement obj, string[] candidateNames, out JsonElement value)
    {
        foreach (var name in candidateNames)
            if (obj.TryGetProperty(name, out value))
                return true;
        value = default;
        return false;
    }

    private static bool TryParseTimestamp(JsonElement el, out DateTimeOffset ts)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String when el.TryGetDateTimeOffset(out ts):
                return true;
            case JsonValueKind.Number when el.TryGetInt64(out var epochMs):
                ts = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
                return true;
            default:
                ts = default;
                return false;
        }
    }

    public static string ShellSingleQuote(string s) =>
        "'" + s.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
