using System.Text.Json;

namespace Web.Json;

// Builds the BacktestSettingsJson payload shared by the create (BacktestDialog) and modify
// (EditInstanceDialog) flows. Every key becomes a cTrader Console CLI backtest option at dispatch time
// (ContainerCommandHelpers maps from→--start, to→--end, dataMode→--data-mode, and any other key→--key),
// so the Advanced field lets the user pass ANY supported backtest option cTrader accepts.
public static class BacktestSettings
{
    public const string DefaultDataMode = "m1";
    public const string DefaultBalance = "10000";

    public static string ToJson(
        DateTime? from, DateTime? to, string? dataMode, string? balance, string? commission,
        string? spread, string? advanced)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (from is { } f) settings["from"] = f.ToString("yyyy-MM-dd");
        if (to is { } t) settings["to"] = t.ToString("yyyy-MM-dd");
        settings["dataMode"] = Blank(dataMode) ? DefaultDataMode : dataMode!.Trim();
        settings["balance"] = Blank(balance) ? DefaultBalance : balance!.Trim();
        settings["commission"] = Blank(commission) ? "0" : commission!.Trim();
        settings["spread"] = Blank(spread) ? "0" : spread!.Trim();

        // Advanced: one "name=value" per line → any other supported cTrader backtest CLI option. A later
        // line overrides an earlier key; an explicit advanced key overrides the named field above it.
        if (!Blank(advanced))
        {
            foreach (var rawLine in advanced!.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim().TrimStart('-');
                var value = line[(eq + 1)..].Trim();
                if (key.Length > 0) settings[key] = value;
            }
        }

        return JsonSerializer.Serialize(settings);
    }

    private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
}
