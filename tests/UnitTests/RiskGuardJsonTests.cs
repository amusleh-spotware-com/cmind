using FluentAssertions;
using Nodes;
using Xunit;

namespace UnitTests;

public sealed class RiskGuardJsonTests
{
    [Fact]
    public void ParseVerdicts_reads_array_of_objects()
    {
        var verdicts = RiskGuardJson.ParseVerdicts(
            "[{\"ref\":0,\"severity\":\"critical\",\"action\":\"stop\",\"reason\":\"blown drawdown\"}," +
            "{\"ref\":1,\"severity\":\"low\",\"action\":\"none\",\"reason\":\"ok\"}]");
        verdicts.Should().HaveCount(2);
        verdicts[0].Ref.Should().Be(0);
        verdicts[0].Reason.Should().Be("blown drawdown");
    }

    [Fact]
    public void ParseVerdicts_handles_fenced_and_empty_array()
    {
        RiskGuardJson.ParseVerdicts("```json\n[]\n```").Should().BeEmpty();
        RiskGuardJson.ParseVerdicts("garbage").Should().BeEmpty();
    }

    [Fact]
    public void ParseVerdicts_skips_entries_without_numeric_ref()
    {
        var verdicts = RiskGuardJson.ParseVerdicts(
            "[{\"severity\":\"critical\",\"action\":\"stop\"},{\"ref\":2,\"action\":\"stop\",\"severity\":\"critical\"}]");
        verdicts.Should().ContainSingle();
        verdicts[0].Ref.Should().Be(2);
    }

    [Fact]
    public void WantsStop_only_true_for_critical_stop()
    {
        RiskGuardJson.WantsStop(new RiskVerdict(0, "critical", "stop", "")).Should().BeTrue();
        RiskGuardJson.WantsStop(new RiskVerdict(0, "high", "stop", "")).Should().BeFalse();
        RiskGuardJson.WantsStop(new RiskVerdict(0, "critical", "none", "")).Should().BeFalse();
    }
}
