using System.Text.Json;
using Core.Ai.CurrencyStrength;
using Core.Domain;

namespace Infrastructure.Ai.CurrencyStrength;

/// <summary>
/// Parses the strict-JSON AI forward gather into Core VOs (trajectories + current gap-fill). Defensive: any
/// malformed/partial/extra-field payload degrades to <see cref="CurrencyForwardGather.Empty"/> — it never
/// throws on the request path, so the caller simply keeps the calendar-only current ranking.
/// </summary>
public static class CurrencyMacroParser
{
    public static CurrencyForwardGather Parse(string? json, CurrencyUniverse universe)
    {
        ArgumentNullException.ThrowIfNull(universe);
        if (string.IsNullOrWhiteSpace(json)) return CurrencyForwardGather.Empty;

        try
        {
            using var doc = JsonDocument.Parse(StripFences(json));
            if (!doc.RootElement.TryGetProperty("currencies", out var currencies)
                || currencies.ValueKind != JsonValueKind.Array)
                return CurrencyForwardGather.Empty;

            var gapFill = new Dictionary<string, CurrencyMacroInputs>(StringComparer.Ordinal);
            var trajectories = new List<CurrencyTrajectory>();

            foreach (var element in currencies.EnumerateArray())
            {
                if (!element.TryGetProperty("code", out var codeProp)) continue;
                var code = codeProp.GetString();
                if (string.IsNullOrWhiteSpace(code) || !universe.Contains(code)) continue;
                var currency = universe.Resolve(code);
                var confidence = ParseConfidence(element);

                if (TryTrajectory(element, currency, confidence, out var trajectory))
                    trajectories.Add(trajectory);

                if (TryGapFill(element, confidence, out var inputs))
                    gapFill[currency.Code] = inputs;
            }

            return new CurrencyForwardGather(gapFill, trajectories);
        }
        catch (JsonException)
        {
            return CurrencyForwardGather.Empty;
        }
    }

    private static bool TryTrajectory(
        JsonElement element, Currency currency, DataConfidence confidence, out CurrencyTrajectory trajectory)
    {
        trajectory = null!;
        if (!element.TryGetProperty("trajectory", out var t) || t.ValueKind != JsonValueKind.Object)
            return false;
        if (Number(t, "ratePathBp") is not { } ratePath) return false;

        try
        {
            trajectory = new CurrencyTrajectory
            {
                Currency = currency,
                ExpectedRatePathBp = ratePath,
                InflationTrend = Number(t, "inflationTrend") ?? 0.0,
                GrowthMomentum = Number(t, "growthMomentum") ?? 0.0,
                GeopoliticalDelta = Number(t, "geopoliticalDelta") ?? 0.0,
                Confidence = confidence
            }.Validate();
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    private static bool TryGapFill(JsonElement element, DataConfidence confidence, out CurrencyMacroInputs inputs)
    {
        inputs = null!;
        if (!element.TryGetProperty("currentGapFill", out var g) || g.ValueKind != JsonValueKind.Object)
            return false;

        inputs = new CurrencyMacroInputs
        {
            PolicyRate = Number(g, "policyRate"),
            Cpi = Number(g, "cpi"),
            GdpGrowth = Number(g, "gdpGrowth"),
            Unemployment = Number(g, "unemployment"),
            RealYield = Number(g, "realYield"),
            ExternalVulnerability = Number(g, "externalVulnerability"),
            PoliticalRisk = Number(g, "politicalRisk"),
            TermsOfTrade = Number(g, "termsOfTrade"),
            Confidence = confidence
        };
        return true;
    }

    private static DataConfidence ParseConfidence(JsonElement element) =>
        element.TryGetProperty("dataConfidence", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() switch
            {
                "High" => DataConfidence.High,
                "Low" => DataConfidence.Low,
                _ => DataConfidence.Medium
            }
            : DataConfidence.Medium;

    private static double? Number(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
            ? d
            : null;

    private static string StripFences(string json)
    {
        var trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;
        var body = trimmed[(firstNewline + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body[..lastFence].Trim() : body.Trim();
    }
}
