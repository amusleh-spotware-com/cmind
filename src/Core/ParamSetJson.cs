using System.Text.Json;

namespace Core;

// The stored JSON for a ParamSet must be a flat "parameter": value map — a single JSON object whose every
// value is a scalar (string / number / bool). That is exactly what ContainerCommandHelpers.JsonToCbotset
// consumes (it wraps the flat map into the cTrader `.cbotset` { "Parameters": { ... } } form). A nested
// object, an array, a null value, a non-object root, or malformed JSON is rejected. An empty object {} is
// valid — it simply means "no overrides; use the cBot's default parameter values".
public static class ParamSetJson
{
    public static bool IsValidSchema(string? jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent)) return false;
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number
                    or JsonValueKind.True or JsonValueKind.False))
                    return false;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
