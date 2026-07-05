using System.Globalization;
using System.Text;
using System.Text.Json;
using Core;
using Core.Constants;

namespace Nodes;

public static class ContainerCommandHelpers
{
    private const string WorkMount = FilePaths.ContainerWorkMount;
    private const string DataDir = FilePaths.ContainerDataDir;
    private const string AlgoFile = FilePaths.CbotAlgoFile;
    private const string PwdFile = FilePaths.CtidPwdFile;
    private const string ReportJson = FilePaths.ReportJsonFile;
    private const string ReportHtml = FilePaths.ReportHtmlFile;

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

    public static string BuildConsoleArgs(Instance i, string ctid)
    {
        var sb = new StringBuilder();
        var isBacktest = i is BacktestInstance;
        sb.Append(isBacktest ? CliCommands.Backtest : CliCommands.Run).Append(' ');
        sb.Append($"{WorkMount}/{AlgoFile} ");
        if (!string.IsNullOrEmpty(ctid))
            sb.Append($"{CliFlags.Ctid} {ctid} {CliFlags.PwdFile} {WorkMount}/{PwdFile} ");
        if (i.TradingAccount is { } ta) sb.Append($"{CliFlags.Account} {ta.AccountNumber} ");
        if (!string.IsNullOrEmpty(i.Symbol)) sb.Append($"{CliFlags.Symbol} {i.Symbol} ");
        if (!string.IsNullOrEmpty(i.Timeframe)) sb.Append($"{CliFlags.Period} {i.Timeframe} ");
        sb.Append($"{CliFlags.DataDir} {DataDir} ");

        if (i is BacktestInstance b && !string.IsNullOrEmpty(b.BacktestSettingsJson))
        {
            var doc = JsonDocument.Parse(b.BacktestSettingsJson);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var name = p.Name.ToLowerInvariant() switch
                {
                    "from" => "start",
                    "to" => "end",
                    _ => p.Name
                };
                var val = p.Value.ValueKind == JsonValueKind.String
                    ? $"\"{p.Value.GetString()}\""
                    : p.Value.ToString();
                sb.Append($"--{name} {val} ");
            }
            sb.Append($"{CliFlags.ReportJson} {WorkMount}/{ReportJson} {CliFlags.Report} {WorkMount}/{ReportHtml} {CliFlags.ExitOnStop} ");
        }
        return sb.ToString().Trim();
    }

    public static string JsonToCbotset(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var sb = new StringBuilder();
            foreach (var p in doc.RootElement.EnumerateObject())
                sb.AppendLine($"{p.Name}={p.Value}");
            return sb.ToString();
        }
        catch { return json; }
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
        long.TryParse(parts[0], out var used);
        long.TryParse(parts[1], out var total);
        return (used, total);
    }

    public static long ParseSize(string s)
    {
        s = s.Trim();
        var mult = 1L;
        if (s.EndsWith("GiB", StringComparison.Ordinal)) { mult = 1024L * 1024 * 1024; s = s[..^3]; }
        else if (s.EndsWith("MiB", StringComparison.Ordinal)) { mult = 1024L * 1024; s = s[..^3]; }
        else if (s.EndsWith("KiB", StringComparison.Ordinal)) { mult = 1024L; s = s[..^3]; }
        else if (s.EndsWith("B", StringComparison.Ordinal)) { s = s[..^1]; }
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
