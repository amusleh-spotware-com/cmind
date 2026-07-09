using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class AccountLinkingTests
{
    [Fact]
    public void Create_marks_account_cid_linked()
    {
        var account = TradingAccount.Create(CtidId.New(), 123456, "Broker", isLive: true, label: null);

        account.LinkMethod.Should().Be(AccountLinkMethod.Cid);
        account.CtidTraderAccountId.Should().BeNull();
    }

    [Fact]
    public void LinkOpenApi_adds_flag_without_dropping_cid()
    {
        var account = TradingAccount.Create(CtidId.New(), 123456, "Broker", isLive: true, label: null);

        account.LinkOpenApi(new CtidTraderAccountId(99), OpenApiAuthorizationId.New());

        account.LinkMethod.Should().Be(AccountLinkMethod.Cid | AccountLinkMethod.OpenApi);
        account.CtidTraderAccountId.Should().Be(99);
        account.OpenApiAuthorizationId.Should().NotBeNull();
    }

    [Fact]
    public void LinkOpenApiAccount_upserts_existing_account_by_number()
    {
        var cid = CTraderIdAccount.CreateForOpenApi(UserId.New(), new CtidUserId(500), "cID 500");
        var authorizationId = OpenApiAuthorizationId.New();

        var first = cid.LinkOpenApiAccount(111, "Broker", true, new CtidTraderAccountId(1), authorizationId, null);
        var second = cid.LinkOpenApiAccount(111, "Broker", true, new CtidTraderAccountId(1), authorizationId, null);

        cid.TradingAccounts.Should().ContainSingle();
        second.Should().BeSameAs(first);
        second.LinkMethod.Should().Be(AccountLinkMethod.OpenApi);
    }

    [Fact]
    public void LinkOpenApiAccount_creates_distinct_accounts_per_number()
    {
        var cid = CTraderIdAccount.CreateForOpenApi(UserId.New(), new CtidUserId(500), "cID 500");
        var authorizationId = OpenApiAuthorizationId.New();

        cid.LinkOpenApiAccount(111, "Broker", true, new CtidTraderAccountId(1), authorizationId, null);
        cid.LinkOpenApiAccount(222, "Broker", false, new CtidTraderAccountId(2), authorizationId, null);

        cid.TradingAccounts.Should().HaveCount(2);
    }

    [Fact]
    public void CreateForOpenApi_sets_grant_identity_and_empty_password()
    {
        var cid = CTraderIdAccount.CreateForOpenApi(UserId.New(), new CtidUserId(777), "cID 777");

        cid.CtidUserId.Should().Be(777);
        cid.EncryptedPassword.Should().BeEmpty();
    }
}
