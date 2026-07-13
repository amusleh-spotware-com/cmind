using Core;
using Core.Accounts;
using Core.Constants;
using Core.CopyTrading;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Accounts;

// Invariants for the CTraderIdAccount aggregate root: create (cID / Open API), username/password updates,
// and owning TradingAccounts through the root with the broker allow-list guard + Open API link dedup.
// (WS-1 Core backfill.)
public class CTraderIdAccountTests
{
    private static CTraderIdAccount NewCid() => CTraderIdAccount.Create(UserId.New(), "trader", [1, 2]);

    [Fact]
    public void Create_and_create_for_open_api_set_the_right_fields()
    {
        var cid = NewCid();
        cid.Username.Should().Be("trader");
        cid.CtidUserId.Should().BeNull();

        var oa = CTraderIdAccount.CreateForOpenApi(UserId.New(), new CtidUserId(555), "oa-trader");
        oa.CtidUserId.Should().Be(555);
        oa.EncryptedPassword.Should().BeEmpty();
    }

    [Fact]
    public void Create_rejects_a_blank_username()
    {
        var act = () => CTraderIdAccount.Create(UserId.New(), " ", [1]);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Update_username_and_password_mutate_and_guard()
    {
        var cid = NewCid();

        cid.UpdateUsername("renamed");
        cid.UpdatePassword([9, 9]);
        cid.Username.Should().Be("renamed");
        cid.EncryptedPassword.Should().BeEquivalentTo(new byte[] { 9, 9 });

        var blank = () => cid.UpdateUsername("");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Add_trading_account_honours_the_broker_allow_list()
    {
        var cid = NewCid();

        var account = cid.AddTradingAccount(12345, "Pepperstone", isLive: false, "demo", BrokerAllowlist.Unrestricted);
        account.AccountNumber.Should().Be(12345);
        account.Broker.Should().Be("Pepperstone");
        cid.TradingAccounts.Should().ContainSingle();

        var restricted = BrokerAllowlist.FromNames(["ICMarkets"]);
        var blocked = () => cid.AddTradingAccount(999, "Pepperstone", false, null, restricted);
        blocked.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.BrokerNotAllowed);
    }

    [Fact]
    public void Link_open_api_account_creates_then_links_existing_without_duplicating()
    {
        var cid = NewCid();
        var authId = new OpenApiAuthorizationId(Guid.NewGuid());

        var created = cid.LinkOpenApiAccount(12345, "Pepperstone", true,
            new CtidTraderAccountId(777), authId, "live", BrokerAllowlist.Unrestricted);
        created.CtidTraderAccountId.Should().Be(777);
        cid.TradingAccounts.Should().ContainSingle();

        // Same account number again -> links the existing row, no duplicate.
        var relinked = cid.LinkOpenApiAccount(12345, "Pepperstone", true,
            new CtidTraderAccountId(888), new OpenApiAuthorizationId(Guid.NewGuid()), "live", BrokerAllowlist.Unrestricted);
        relinked.Should().BeSameAs(created);
        cid.TradingAccounts.Should().ContainSingle("re-linking an existing account number must not add a row");
    }
}
