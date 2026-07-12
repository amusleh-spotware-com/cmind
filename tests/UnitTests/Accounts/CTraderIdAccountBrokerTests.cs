using Core;
using Core.Accounts;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Accounts;

public sealed class CTraderIdAccountBrokerTests
{
    private static CTraderIdAccount NewCid() =>
        CTraderIdAccount.Create(UserId.New(), "ct-user", "pw"u8.ToArray());

    [Fact]
    public void AddTradingAccount_allows_any_broker_when_unrestricted()
    {
        var cid = NewCid();

        var account = cid.AddTradingAccount(123, "Some Broker", isLive: false, label: null, BrokerAllowlist.Unrestricted);

        account.Broker.Should().Be("Some Broker");
    }

    [Fact]
    public void AddTradingAccount_rejects_disallowed_broker_when_restricted()
    {
        var cid = NewCid();
        var allowlist = BrokerAllowlist.FromNames(["Pepperstone"]);

        var act = () => cid.AddTradingAccount(123, "Other Broker", isLive: false, label: null, allowlist);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.BrokerNotAllowed);
        cid.TradingAccounts.Should().BeEmpty();
    }

    [Fact]
    public void AddTradingAccount_accepts_allowed_broker_when_restricted()
    {
        var cid = NewCid();
        var allowlist = BrokerAllowlist.FromNames(["Pepperstone"]);

        var account = cid.AddTradingAccount(123, "pepperstone", isLive: true, label: null, allowlist);

        account.Broker.Should().Be("pepperstone");
        cid.TradingAccounts.Should().ContainSingle();
    }

    [Fact]
    public void LinkOpenApiAccount_rejects_disallowed_broker_when_restricted()
    {
        var cid = CTraderIdAccount.CreateForOpenApi(UserId.New(), new CtidUserId(1), "cID 1");
        var allowlist = BrokerAllowlist.FromNames(["Pepperstone"]);

        var act = () => cid.LinkOpenApiAccount(111, "Other", true,
            new CtidTraderAccountId(1), OpenApiAuthorizationId.New(), null, allowlist);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.BrokerNotAllowed);
    }
}
