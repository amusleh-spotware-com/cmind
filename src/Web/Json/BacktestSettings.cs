using System.Text.Json;

namespace Web.Json;

// Builds the BacktestSettingsJson payload shared by the create (BacktestDialog) and modify
// (EditInstanceDialog) flows. Every key becomes a cTrader Console CLI backtest option at dispatch time
// (ContainerCommandHelpers maps from→--start, to→--end, dataMode→--data-mode, dataFile→--data-file,
// environmentVariables→the --environment-variables flag, and any other key→--key value).
public static class BacktestSettings
{
    public const string DefaultDataMode = "m1";
    public const string DefaultBalance = "10000";

    public static string ToJson(
        DateTime? from, DateTime? to, string? dataMode, string? balance, string? commission,
        string? spread, string? dataFile, bool environmentVariables)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (from is { } f) settings["from"] = f.ToString("yyyy-MM-dd");
        if (to is { } t) settings["to"] = t.ToString("yyyy-MM-dd");
        settings["dataMode"] = Blank(dataMode) ? DefaultDataMode : dataMode!.Trim();
        settings["balance"] = Blank(balance) ? DefaultBalance : balance!.Trim();
        settings["commission"] = Blank(commission) ? "0" : commission!.Trim();
        settings["spread"] = Blank(spread) ? "0" : spread!.Trim();
        if (!Blank(dataFile)) settings["dataFile"] = dataFile!.Trim();
        if (environmentVariables) settings["environmentVariables"] = "true";
        return JsonSerializer.Serialize(settings);
    }

    private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
}
