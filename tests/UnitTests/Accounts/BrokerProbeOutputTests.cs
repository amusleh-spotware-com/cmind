using Core.Accounts;
using FluentAssertions;
using Xunit;

namespace UnitTests.Accounts;

public sealed class BrokerProbeOutputTests
{
    [Fact]
    public void Parses_broker_from_marked_line()
    {
        var ok = BrokerProbeOutput.TryParseBroker("2026-07-12 ##CMIND-BROKER##Pepperstone##END## done", out var broker);

        ok.Should().BeTrue();
        broker.Value.Should().Be("Pepperstone");
    }

    [Theory]
    [InlineData("just some log line")]
    [InlineData("##CMIND-BROKER####END##")]
    [InlineData("##CMIND-BROKER##no end marker")]
    public void Returns_false_for_non_marker_or_empty_value(string line)
    {
        BrokerProbeOutput.TryParseBroker(line, out _).Should().BeFalse();
    }

    [Fact]
    public void Detects_login_failure_text()
    {
        BrokerProbeOutput.IndicatesLoginFailure("Error: authentication failed for user").Should().BeTrue();
        BrokerProbeOutput.IndicatesLoginFailure("started ok").Should().BeFalse();
    }
}
