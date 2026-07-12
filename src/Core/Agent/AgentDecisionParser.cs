using System.Text.Json;
using Core.Execution;

namespace Core.Agent;

/// <summary>The structured intent parsed from a model reply, before it is bound to a specific account.</summary>
public sealed record ParsedAgentAction(string Reasoning, string? Side, string? Symbol, double? SizeLots, IReadOnlyList<string> Evidence);

/// <summary>
/// Deterministically parses a model reply into an <see cref="AgentDecision"/> for a target account. The
/// model is asked to return JSON <c>{ reasoning, action: buy|sell|hold, symbol, sizeLots, evidence[] }</c>;
/// anything malformed or non-actionable degrades to a Hold carrying the raw text as reasoning, so a bad
/// completion can never fabricate an order. Pure — the LLM call itself lives behind the engine.
/// </summary>
public static class AgentDecisionParser
{
    public static ParsedAgentAction Parse(string? modelText)
    {
        if (string.IsNullOrWhiteSpace(modelText)) return new ParsedAgentAction("No decision.", null, null, null, []);

        var json = StripFences(modelText);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Hold(modelText);

            var reasoning = GetString(root, "reasoning") ?? modelText.Trim();
            var action = GetString(root, "action")?.Trim().ToLowerInvariant();
            var symbol = GetString(root, "symbol");
            double? size = root.TryGetProperty("sizeLots", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetDouble() : null;
            var evidence = ParseEvidence(root);

            var side = action switch { "buy" => "Buy", "sell" => "Sell", _ => null };
            return new ParsedAgentAction(reasoning, side, symbol, size, evidence);
        }
        catch (JsonException)
        {
            return Hold(modelText);
        }
    }

    /// <summary>Binds a parsed action to a concrete account, producing the decision (Hold when not actionable).</summary>
    public static AgentDecision ToDecision(ParsedAgentAction action, TradingAccountId account)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Side is null || string.IsNullOrWhiteSpace(action.Symbol) || action.SizeLots is not { } size || size <= 0)
            return new AgentDecision(action.Reasoning, Order: null, Evidence: action.Evidence);

        var side = action.Side.Equals("Sell", StringComparison.OrdinalIgnoreCase) ? OrderSide.Sell : OrderSide.Buy;
        return new AgentDecision(action.Reasoning, new AgentOrderIntent(account, action.Symbol!.Trim().ToUpperInvariant(), side, size), action.Evidence);
    }

    private static ParsedAgentAction Hold(string text) => new(text.Trim(), null, null, null, []);

    private static string StripFences(string value)
    {
        var s = value.Trim();
        if (!s.StartsWith("```", StringComparison.Ordinal))
        {
            var open = s.IndexOf('{');
            var close = s.LastIndexOf('}');
            return open >= 0 && close > open ? s[open..(close + 1)] : s;
        }
        var nl = s.IndexOf('\n');
        if (nl >= 0) s = s[(nl + 1)..];
        if (s.EndsWith("```", StringComparison.Ordinal)) s = s[..^3];
        return s.Trim();
    }

    private static string? GetString(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static List<string> ParseEvidence(JsonElement root)
    {
        if (!root.TryGetProperty("evidence", out var e) || e.ValueKind != JsonValueKind.Array) return [];
        var list = new List<string>();
        foreach (var item in e.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } str)
                list.Add(str);
        return list;
    }
}
