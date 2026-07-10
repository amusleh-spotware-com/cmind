using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OpenApiAuthDomainTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenApiClientId_rejects_blank(string value)
    {
        var act = () => _ = new OpenApiClientId(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.OpenApiClientIdRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CtidTraderAccountId_rejects_non_positive(long value)
    {
        var act = () => _ = new CtidTraderAccountId(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CtidTraderAccountInvalid);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/cb")]
    [InlineData("")]
    public void OpenApiRedirectUri_rejects_invalid(string value)
    {
        var act = () => _ = new OpenApiRedirectUri(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.OpenApiRedirectUriInvalid);
    }

    [Fact]
    public void Application_Create_rejects_empty_secret()
    {
        var act = () => OpenApiApplication.Create(UserId.New(), "app",
            new OpenApiClientId("cid"), [], new OpenApiRedirectUri("https://app.test/cb"));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.OpenApiSecretRequired);
    }

    [Fact]
    public void Authorization_Create_raises_authorized_event()
    {
        var authorization = CreateAuthorization(TestClock.Now.AddDays(30));

        authorization.DomainEvents.OfType<OpenApiAccountAuthorized>().Should().ContainSingle();
        authorization.RefreshFailedAt.Should().BeNull();
    }

    [Fact]
    public void IsExpiring_true_only_within_threshold()
    {
        var now = TestClock.Now;
        var authorization = CreateAuthorization(now.AddDays(30));

        authorization.IsExpiring(TimeSpan.FromDays(1), now).Should().BeFalse();
        authorization.IsExpiring(TimeSpan.FromDays(31), now).Should().BeTrue();
    }

    [Fact]
    public void Refresh_rotates_tokens_clears_failure_and_raises_event()
    {
        var authorization = CreateAuthorization(TestClock.Now.AddMinutes(1));
        authorization.MarkRefreshFailed("boom", TestClock.Now);
        authorization.RefreshFailedAt.Should().NotBeNull();

        var newExpiry = TestClock.Now.AddDays(30);
        authorization.Refresh([9, 9], [8, 8], newExpiry, TestClock.Now);

        authorization.EncryptedAccessToken.Should().Equal(new byte[] { 9, 9 });
        authorization.EncryptedRefreshToken.Should().Equal(new byte[] { 8, 8 });
        authorization.AccessTokenExpiresAt.Should().Be(newExpiry);
        authorization.RefreshFailedAt.Should().BeNull();
        authorization.LastRefreshedAt.Should().NotBeNull();
        authorization.DomainEvents.OfType<AccessTokenRefreshed>().Should().ContainSingle();
    }

    [Fact]
    public void MarkRefreshFailed_raises_failed_event()
    {
        var authorization = CreateAuthorization(TestClock.Now.AddMinutes(1));

        authorization.MarkRefreshFailed("network down", TestClock.Now);

        authorization.DomainEvents.OfType<AccessTokenRefreshFailed>().Should().ContainSingle()
            .Which.Reason.Should().Be("network down");
    }

    private static OpenApiAuthorization CreateAuthorization(DateTimeOffset expiresAt)
        => OpenApiAuthorization.Create(
            UserId.New(),
            OpenApiApplicationId.New(),
            new CtidUserId(12345),
            isLive: true,
            [1, 2, 3],
            [4, 5, 6],
            expiresAt,
            OpenApiScope.Trade);
}
