using System.Dynamic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core;

namespace Web.Json;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions DynamicWeb = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new StrongIdJsonConverterFactory());
        o.Converters.Add(new ObjectToInferredTypesConverter());
        return o;
    }

    public static async Task<dynamic[]> GetDynamicArrayAsync(this HttpClient http, string url)
    {
        var list = await http.GetFromJsonAsync<List<object?>>(url, DynamicWeb);
        return list?.Cast<dynamic>().ToArray() ?? [];
    }

    public static async Task<dynamic?> GetDynamicAsync(this HttpClient http, string url)
    {
        var obj = await http.GetFromJsonAsync<object?>(url, DynamicWeb);
        return obj;
    }
}

internal sealed class ObjectToInferredTypesConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetGuid(out var g) => g,
            JsonTokenType.String when reader.TryGetDateTimeOffset(out var d) => d,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);

    private ExpandoObject ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var expando = new ExpandoObject();
        IDictionary<string, object?> dict = expando;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            dict[key] = Read(ref reader, typeof(object), options);
        }
        return expando;
    }

    private List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            list.Add(Read(ref reader, typeof(object), options));
        return list;
    }
}
