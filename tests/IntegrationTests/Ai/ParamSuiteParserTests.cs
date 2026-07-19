using System.Text.Json;
using FluentAssertions;
using Web.Ai;
using Xunit;

namespace IntegrationTests.Ai;

// The AI-optimize param-suite parser must recover parameter sets from the many shapes small local models
// emit (the reported "AI returned no usable parameter sets" was a too-strict parser), not only a clean array.
public sealed class ParamSuiteParserTests
{
    [Fact]
    public void Parses_the_requested_json_array()
    {
        var text = """[{"name":"A","parameters":{"period":14}},{"name":"B","parameters":{"period":21}}]""";
        var sets = ParamSuiteParser.Parse(text, 3);
        sets.Should().HaveCount(2);
        sets[0].Name.Should().Be("A");
        JsonDocument.Parse(sets[0].Json).RootElement.GetProperty("period").GetInt32().Should().Be(14);
    }

    [Fact]
    public void Parses_a_fenced_array_embedded_in_prose()
    {
        var text = "Here are the sets:\n```json\n[{\"name\":\"X\",\"parameters\":{\"tp\":30}}]\n```\nGood luck.";
        var sets = ParamSuiteParser.Parse(text, 3);
        sets.Should().HaveCount(1);
        sets[0].Name.Should().Be("X");
    }

    [Fact]
    public void Recovers_concatenated_objects_without_an_array_wrapper()
    {
        // A small model emits several objects, no [] wrapper, with prose in between.
        var text = "Set one: {\"name\":\"One\",\"parameters\":{\"period\":10}} and set two {\"name\":\"Two\",\"parameters\":{\"period\":20}}";
        var sets = ParamSuiteParser.Parse(text, 3);
        sets.Should().HaveCount(2);
        sets.Select(s => s.Name).Should().Contain(["One", "Two"]);
    }

    [Fact]
    public void Treats_a_flat_object_as_the_parameter_map_and_strips_the_name_key()
    {
        var text = """{"name":"Flat","period":14,"stopLoss":50}""";
        var sets = ParamSuiteParser.Parse(text, 3);
        sets.Should().HaveCount(1);
        sets[0].Name.Should().Be("Flat");
        var map = JsonDocument.Parse(sets[0].Json).RootElement;
        map.TryGetProperty("name", out _).Should().BeFalse("the name is metadata, not a cBot parameter");
        map.GetProperty("period").GetInt32().Should().Be(14);
    }

    [Fact]
    public void Honours_the_max_count()
    {
        var text = """[{"parameters":{"a":1}},{"parameters":{"a":2}},{"parameters":{"a":3}}]""";
        ParamSuiteParser.Parse(text, 2).Should().HaveCount(2);
    }

    [Fact]
    public void Returns_empty_for_prose_with_no_json()
    {
        ParamSuiteParser.Parse("I could not produce parameter sets for this strategy.", 3).Should().BeEmpty();
    }
}
