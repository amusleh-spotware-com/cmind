using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.OpenApi;
using Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OAuthStateServiceTests
{
    private static IOAuthStateService Create()
    {
        var provider = new ServiceCollection().AddDataProtection().Services
            .BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        return new OAuthStateService(new DataProtectionSecretProtector(provider));
    }

    [Fact]
    public void Round_trips_user_application_and_invite_flag()
    {
        var service = Create();
        var userId = UserId.New();
        var applicationId = OpenApiApplicationId.New();

        var state = service.CreateState(userId, applicationId, TimeSpan.FromMinutes(10), isInvite: true);
        var result = service.Validate(state);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.ApplicationId.Should().Be(applicationId);
        result.IsInvite.Should().BeTrue();
    }

    [Fact]
    public void Tampered_state_is_rejected()
    {
        var service = Create();
        var state = service.CreateState(UserId.New(), OpenApiApplicationId.New(), TimeSpan.FromMinutes(10), false);

        var tampered = state[..^2] + (state[^1] == 'A' ? "BB" : "AA");

        service.Validate(tampered).Should().BeNull();
    }

    [Fact]
    public void Expired_state_is_rejected()
    {
        var service = Create();
        var state = service.CreateState(UserId.New(), OpenApiApplicationId.New(), TimeSpan.FromSeconds(-1), false);

        service.Validate(state).Should().BeNull();
    }

    [Fact]
    public void Garbage_state_is_rejected() => Create().Validate("not-a-valid-token").Should().BeNull();
}
