using System.Text.Json;
using Core.Agent;
using Core.Constants;

namespace Nodes.Agent;

public static class AgentJson
{
    public static AgentAction? ParseAction(string text)
    {
        foreach (var candidate in new[] { StripFences(text), BraceSlice(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (!root.TryGetProperty("parameters", out var p) || p.ValueKind != JsonValueKind.Object) continue;

                var reasoning = root.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String
                    ? r.GetString()!
                    : string.Empty;
                var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString()!
                    : "AI proposal";
                return new AgentAction(Clip(reasoning), Clip(name, 128), p.GetRawText());
            }
            catch (JsonException) { /* try next candidate */ }
        }
        return null;
    }

    private static string StripFences(string value)
    {
        var s = value.Trim();
        if (!s.StartsWith("```", StringComparison.Ordinal)) return s;
        var nl = s.IndexOf('\n');
        if (nl >= 0) s = s[(nl + 1)..];
        if (s.EndsWith("```", StringComparison.Ordinal)) s = s[..^3];
        return s.Trim();
    }

    private static string BraceSlice(string value)
    {
        var open = value.IndexOf('{');
        var close = value.LastIndexOf('}');
        return open >= 0 && close > open ? value[open..(close + 1)] : string.Empty;
    }

    private static string Clip(string value, int max = AgentConstants.MaxReasoningChars) =>
        value.Length <= max ? value : value[..max];
}
