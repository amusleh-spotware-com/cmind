using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai;
using NSubstitute;
using Xunit;

namespace UnitTests.Ai;

public class AiFeatureServiceRecommendTests
{
    [Fact]
    public async Task RecommendCopyProfile_forwards_risk_profile_and_source_to_client()
    {
        var client = Substitute.For<IAiClient>();
        client.CompleteAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(AiResult.Ok("{\"riskMode\":\"LotMultiplier\"}"));
        var service = new AiFeatureService(client);

        var result = await service.RecommendCopyProfileAsync("conservative", "EURUSD scalper", CancellationToken.None);

        result.Success.Should().BeTrue();
        await client.Received(1).CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.System.Contains("copy-trading risk configurator")
                                       && r.User.Contains("conservative")
                                       && r.User.Contains("EURUSD scalper")),
            Arg.Any<CancellationToken>());
    }
}
