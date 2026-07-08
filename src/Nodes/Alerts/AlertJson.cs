using System.Text.Json;
using Core.Constants;

namespace Nodes.Alerts;

public sealed record AlertAssessment(bool Alert, string Severity, string Message);

public static class AlertJson
{
    private static readonly HashSet<string> Severities =
        new(StringComparer.OrdinalIgnoreCase) { AlertConstants.SeverityInfo, AlertConstants.SeverityWarning, AlertConstants.SeverityCritical };

    public static AlertAssessment? Parse(string text)
    {
        foreach (var candidate in new[] { StripFences(text), BraceSlice(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (!root.TryGetProperty("alert", out var a) ||
                    a.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    continue;

                var severity = root.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.String
                    ? s.GetString()!
                    : AlertConstants.SeverityInfo;
                if (!Severities.Contains(severity)) severity = AlertConstants.SeverityInfo;

                var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? Clip(m.GetString()!)
                    : string.Empty;
                return new AlertAssessment(a.GetBoolean(), severity.ToLowerInvariant(), message);
            }
            catch (JsonException) { /* try next candidate */ }
        }
        return null;
    }

    private static string StripFences(string value)
    {
        var v = value.Trim();
        if (!v.StartsWith("```", StringComparison.Ordinal)) return v;
        var nl = v.IndexOf('\n');
        if (nl >= 0) v = v[(nl + 1)..];
        if (v.EndsWith("```", StringComparison.Ordinal)) v = v[..^3];
        return v.Trim();
    }

    private static string BraceSlice(string value)
    {
        var open = value.IndexOf('{');
        var close = value.LastIndexOf('}');
        return open >= 0 && close > open ? value[open..(close + 1)] : string.Empty;
    }

    private static string Clip(string value) =>
        value.Length <= AlertConstants.MaxMessageChars ? value : value[..AlertConstants.MaxMessageChars];
}
