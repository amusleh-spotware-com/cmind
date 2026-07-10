using Core;
using Core.Domain;
using CopyEngine;
using FluentAssertions;
using Nodes.CopyTrading;
using Xunit;

namespace IntegrationTests.CopyLive;

// The supervisor detects an access-token rotation by comparing a token signature on the running host
// against a freshly-built plan; a change triggers a seamless restart. These assert the signature
// tracks exactly the source + destination tokens.
public sealed class TokenRotationSignatureTests
{
    private static CopyProfilePlan Plan(string sourceToken, string destToken)
        => new(CopyProfileId.New(), Live: false, "client", "secret", 100, sourceToken, 1,
            [new CopyDestinationPlan(200, destToken, 1, Destination())]);

    private static CopyDestination Destination()
        => CopyProfile.Create(UserId.New(), "p", TradingAccountId.New())
            .AddDestination(TradingAccountId.New(), RiskSettings.Default);

    [Fact]
    public void Signature_changes_when_the_source_token_rotates()
    {
        CopyEngineSupervisor.TokenSignature(Plan("old", "d"))
            .Should().NotBe(CopyEngineSupervisor.TokenSignature(Plan("new", "d")));
    }

    [Fact]
    public void Signature_changes_when_a_destination_token_rotates()
    {
        CopyEngineSupervisor.TokenSignature(Plan("s", "old"))
            .Should().NotBe(CopyEngineSupervisor.TokenSignature(Plan("s", "new")));
    }

    [Fact]
    public void Signature_is_stable_when_tokens_are_unchanged()
    {
        CopyEngineSupervisor.TokenSignature(Plan("s", "d"))
            .Should().Be(CopyEngineSupervisor.TokenSignature(Plan("s", "d")));
    }
}
