using System.Text.Json;
using Core.Constants;

namespace Nodes;

public sealed record RiskVerdict(int Ref, string Severity, string Action, string Reason);

public static class RiskGuardJson
{
    public static IReadOnlyList<RiskVerdict> ParseVerdicts(string text)
    {
        foreach (var candidate in new[] { StripFences(text), BracketSlice(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                var results = new List<RiskVerdict>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object) continue;
                    if (!element.TryGetProperty("ref", out var r) || r.ValueKind != JsonValueKind.Number) continue;
                    if (!r.TryGetInt32(out var refIndex) || refIndex < 0) continue;

                    var severity = StringOr(element, "severity", "low").ToLowerInvariant();
                    var action = StringOr(element, "action", "none").ToLowerInvariant();
                    var reason = Clip(StringOr(element, "reason", string.Empty));
                    results.Add(new RiskVerdict(refIndex, severity, action, reason));
                }
                return results;
            }
            catch (JsonException) { /* try next candidate */ }
        }
        return [];
    }

    public static bool WantsStop(RiskVerdict verdict) =>
        string.Equals(verdict.Action, RiskGuardConstants.ActionStop, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(verdict.Severity, RiskGuardConstants.SeverityCritical, StringComparison.OrdinalIgnoreCase);

    private static string StringOr(JsonElement element, string name, string fallback) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;

    private static string StripFences(string value)
    {
        var s = value.Trim();
        if (!s.StartsWith("```", StringComparison.Ordinal)) return s;
        var nl = s.IndexOf('\n');
        if (nl >= 0) s = s[(nl + 1)..];
        if (s.EndsWith("```", StringComparison.Ordinal)) s = s[..^3];
        return s.Trim();
    }

    private static string BracketSlice(string value)
    {
        var open = value.IndexOf('[');
        var close = value.LastIndexOf(']');
        return open >= 0 && close > open ? value[open..(close + 1)] : string.Empty;
    }

    private static string Clip(string value) =>
        value.Length <= RiskGuardConstants.MaxReasonChars ? value : value[..RiskGuardConstants.MaxReasonChars];
}
