using System.Text.Json;
using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

// JSON round-trip for strongly-typed ids via the converter factory: string-guid + object forms in,
// string out, invalid tokens throw; factory recognizes only strong-id value types. (WS-1 Core backfill.)
public class StrongIdJsonConverterTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new StrongIdJsonConverterFactory());
        return options;
    }

    [Fact]
    public void Round_trips_a_strong_id_as_a_json_string()
    {
        var options = Options();
        var id = UserId.New();

        var json = JsonSerializer.Serialize(id, options);
        json.Should().Be($"\"{id.Value}\"", "the id serializes as a bare guid string");

        JsonSerializer.Deserialize<UserId>(json, options).Should().Be(id);
    }

    [Fact]
    public void Reads_the_legacy_object_form_with_a_value_property()
    {
        var options = Options();
        var id = TradingAccountId.New();

        var back = JsonSerializer.Deserialize<TradingAccountId>($"{{\"Value\":\"{id.Value}\"}}", options);

        back.Should().Be(id);
    }

    [Fact]
    public void Rejects_an_invalid_token()
    {
        var options = Options();
        var act = () => JsonSerializer.Deserialize<UserId>("123", options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Factory_converts_only_strong_id_value_types()
    {
        var factory = new StrongIdJsonConverterFactory();

        factory.CanConvert(typeof(UserId)).Should().BeTrue();
        factory.CanConvert(typeof(CBotId)).Should().BeTrue();
        factory.CanConvert(typeof(int)).Should().BeFalse("a plain value type is not a strong id");
        factory.CanConvert(typeof(string)).Should().BeFalse();
    }
}
