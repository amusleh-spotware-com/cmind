using FluentAssertions;
using Nodes.Alerts;
using Xunit;

namespace UnitTests.Alerts;

public sealed class AlertJsonTests
{
    [Fact]
    public void Parse_reads_alert_severity_and_message()
    {
        var a = AlertJson.Parse("{\"alert\":true,\"severity\":\"critical\",\"message\":\"ECB surprise cut\"}");
        a.Should().NotBeNull();
        a!.Alert.Should().BeTrue();
        a.Severity.Should().Be("critical");
        a.Message.Should().Be("ECB surprise cut");
    }

    [Fact]
    public void Parse_strips_fences_and_defaults_unknown_severity()
    {
        var a = AlertJson.Parse("```json\n{\"alert\":true,\"severity\":\"bogus\",\"message\":\"x\"}\n```");
        a.Should().NotBeNull();
        a!.Severity.Should().Be("info");
    }

    [Fact]
    public void Parse_handles_false_alert()
    {
        var a = AlertJson.Parse("{\"alert\":false,\"severity\":\"info\",\"message\":\"\"}");
        a.Should().NotBeNull();
        a!.Alert.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"severity\":\"info\"}")]
    [InlineData("[1,2]")]
    public void Parse_returns_null_for_unusable_input(string input)
    {
        AlertJson.Parse(input).Should().BeNull();
    }
}
