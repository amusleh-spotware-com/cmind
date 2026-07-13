using Core;
using Core.Access;
using Core.Agent;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

// DisposableEmailDomains block-list check + AgentMemoryRecord creation/clipping. (WS-1 Core backfill.)
public class AccessAndAgentMemoryTests
{
    [Theory]
    [InlineData("bob@mailinator.com", true)]
    [InlineData("bob@MAILINATOR.COM", true)]
    [InlineData("bob@gmail.com", false)]
    [InlineData("not-an-email", false)]
    [InlineData("trailing@", false)]
    public void Is_disposable_detects_throwaway_domains(string email, bool expected)
    {
        DisposableEmailDomains.IsDisposable(email).Should().Be(expected);
    }

    [Fact]
    public void Agent_memory_create_stores_the_content()
    {
        var agentId = TradingAgentId.New();
        var userId = UserId.New();

        var memory = AgentMemoryRecord.Create(agentId, userId, MemoryTier.MarketIntelligence, "GBP spiked on CPI");

        memory.AgentId.Should().Be(agentId);
        memory.UserId.Should().Be(userId);
        memory.Tier.Should().Be(MemoryTier.MarketIntelligence);
        memory.Content.Should().Be("GBP spiked on CPI");
    }

    [Fact]
    public void Agent_memory_rejects_blank_and_clips_to_the_limit()
    {
        var blank = () => AgentMemoryRecord.Create(TradingAgentId.New(), UserId.New(), MemoryTier.LowLevelReflection, "  ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AgentMemoryContentRequired);

        var big = new string('x', 3000);
        var memory = AgentMemoryRecord.Create(TradingAgentId.New(), UserId.New(), MemoryTier.HighLevelReflection, big);
        memory.Content.Length.Should().Be(2000, "content is clipped to the 2000-char limit");
    }
}
