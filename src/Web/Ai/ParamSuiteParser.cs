using System.Text.Json;

namespace Web.Ai;

/// <summary>
/// Parses an AI "parameter suite" completion into concrete cBot parameter sets. The requested shape is a
/// JSON array of <c>{ name, parameters }</c> objects, but small local models frequently ignore that and
/// emit a single object, several concatenated objects, or objects interleaved with prose — so this recovers
/// parameter sets from any of those forms rather than failing outright.
/// </summary>
public static class ParamSuiteParser
{
    public static List<(string Name, string Json)> Parse(string text, int max)
    {
        var results = new List<(string, string)>();

        // First: a proper JSON array (the requested shape), possibly fenced or embedded in prose.
        foreach (var candidate in new[] { StripFences(text), BracketSlice(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (results.Count >= max) break;
                    AddParamSet(results, element);
                }
                if (results.Count > 0) return results;
            }
            catch (JsonException) { /* try next candidate */ }
        }

        // Fallback for small local models that ignore "output a JSON array": recover any balanced top-level
        // JSON object(s) from the text and treat each as a parameter set. Handles a single object, several
        // concatenated objects, and objects interleaved with prose.
        foreach (var objectText in ExtractJsonObjects(text))
        {
            if (results.Count >= max) break;
            try
            {
                using var doc = JsonDocument.Parse(objectText);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    AddParamSet(results, doc.RootElement);
            }
            catch (JsonException) { /* skip a non-JSON brace run */ }
        }
        return results;
    }

    private static void AddParamSet(List<(string Name, string Json)> results, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        var name = element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!
            : $"AI-opt {results.Count + 1}";
        // Prefer an explicit "parameters" object; otherwise treat the object itself as the parameter map
        // (minus the "name" key, which is metadata, not a cBot parameter).
        if (element.TryGetProperty("parameters", out var p) && p.ValueKind == JsonValueKind.Object)
        {
            results.Add((name, p.GetRawText()));
            return;
        }
        var map = new Dictionary<string, JsonElement>();
        foreach (var prop in element.EnumerateObject())
            if (!prop.NameEquals("name")) map[prop.Name] = prop.Value;
        if (map.Count > 0) results.Add((name, JsonSerializer.Serialize(map)));
    }

    // Scan for balanced { } runs at brace-depth 1, tolerating braces inside JSON strings.
    private static IEnumerable<string> ExtractJsonObjects(string text)
    {
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{':
                    if (depth == 0) start = i;
                    depth++;
                    break;
                case '}':
                    if (depth > 0 && --depth == 0 && start >= 0)
                        yield return text[start..(i + 1)];
                    break;
            }
        }
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

    private static string BracketSlice(string value)
    {
        var open = value.IndexOf('[');
        var close = value.LastIndexOf(']');
        return open >= 0 && close > open ? value[open..(close + 1)] : string.Empty;
    }
}
