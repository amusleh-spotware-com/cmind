using FluentAssertions;
using Nodes.Agent;
using Xunit;

namespace UnitTests.Agent;

public sealed class AgentJsonTests
{
    [Fact]
    public void ParseAction_reads_reasoning_name_and_parameters()
    {
        var action = AgentJson.ParseAction(
            "{\"reasoning\":\"tighten stop\",\"name\":\"Tighter SL\",\"parameters\":{\"StopLoss\":20}}");
        action.Should().NotBeNull();
        action!.Reasoning.Should().Be("tighten stop");
        action.Name.Should().Be("Tighter SL");
        action.ParametersJson.Should().Contain("StopLoss");
    }

    [Fact]
    public void ParseAction_strips_code_fences()
    {
        var action = AgentJson.ParseAction(
            "```json\n{\"reasoning\":\"x\",\"name\":\"n\",\"parameters\":{\"A\":1}}\n```");
        action.Should().NotBeNull();
        action!.ParametersJson.Should().Contain("A");
    }

    [Fact]
    public void ParseAction_extracts_object_embedded_in_prose()
    {
        var action = AgentJson.ParseAction(
            "Here is my plan: {\"reasoning\":\"y\",\"name\":\"n\",\"parameters\":{\"B\":2}} — done.");
        action.Should().NotBeNull();
        action!.ParametersJson.Should().Contain("B");
    }

    [Fact]
    public void ParseAction_defaults_missing_name_and_reasoning()
    {
        var action = AgentJson.ParseAction("{\"parameters\":{\"C\":3}}");
        action.Should().NotBeNull();
        action!.Name.Should().Be("AI proposal");
        action.Reasoning.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"reasoning\":\"x\",\"name\":\"n\"}")]
    [InlineData("{\"parameters\":\"not an object\"}")]
    [InlineData("[1,2,3]")]
    public void ParseAction_returns_null_for_unusable_input(string input)
    {
        AgentJson.ParseAction(input).Should().BeNull();
    }
}
