using System.Security.Cryptography;
using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

public class UserRegistrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    private static AppUser Register(int roleRank = 2,
        UserActivationState state = UserActivationState.PendingApproval)
        => AppUser.SelfRegister(roleRank, new Email("new@user.local"), "hash",
            RandomNumberGenerator.GetBytes(32), UserProfile.Create(fullName: "New User"), state);

    [Fact]
    public void Self_register_user_starts_pending_and_not_password_change()
    {
        var user = Register();
        user.Should().BeOfType<RegularUser>();
        user.ActivationState.Should().Be(UserActivationState.PendingApproval);
        user.IsActive.Should().BeFalse();
        user.MustChangePassword.Should().BeFalse();
        user.Profile.FullName.Should().Be("New User");
        user.DomainEvents.OfType<UserRegistered>().Should().ContainSingle();
    }

    [Fact]
    public void Self_register_viewer_allowed()
        => Register(roleRank: 3).Should().BeOfType<ViewerUser>();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Self_register_refuses_owner_and_admin(int roleRank)
    {
        var act = () => Register(roleRank);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.RegistrationRoleNotAllowed);
    }

    [Fact]
    public void Approve_activates_pending_account()
    {
        var user = Register();
        user.Approve();
        user.IsActive.Should().BeTrue();
        user.DomainEvents.OfType<UserApproved>().Should().ContainSingle();
    }

    [Fact]
    public void Approve_on_active_account_throws()
    {
        var user = Register(state: UserActivationState.Active);
        var act = () => user.Approve();
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.UserActivationTransitionInvalid);
    }

    [Fact]
    public void Email_verification_token_activates_account()
    {
        var user = Register(state: UserActivationState.PendingEmailVerification);
        user.IssueEmailVerificationToken("hash-1", Now.AddHours(24));

        user.RedeemEmailVerificationToken("hash-1", Now).Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.DomainEvents.OfType<UserEmailConfirmed>().Should().ContainSingle();
    }

    [Fact]
    public void Email_verification_token_is_single_use()
    {
        var user = Register(state: UserActivationState.PendingEmailVerification);
        user.IssueEmailVerificationToken("hash-1", Now.AddHours(24));

        user.RedeemEmailVerificationToken("hash-1", Now).Should().BeTrue();
        user.RedeemEmailVerificationToken("hash-1", Now).Should().BeFalse("account already active");
    }

    [Fact]
    public void Email_verification_rejects_wrong_or_expired_token()
    {
        var user = Register(state: UserActivationState.PendingEmailVerification);
        user.IssueEmailVerificationToken("hash-1", Now.AddHours(24));

        user.RedeemEmailVerificationToken("wrong", Now).Should().BeFalse();
        user.RedeemEmailVerificationToken("hash-1", Now.AddHours(25)).Should().BeFalse("token expired");
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Issuing_new_token_invalidates_the_previous_one()
    {
        var user = Register(state: UserActivationState.PendingEmailVerification);
        user.IssueEmailVerificationToken("old", Now.AddHours(24));
        user.IssueEmailVerificationToken("new", Now.AddHours(24));

        user.RedeemEmailVerificationToken("old", Now).Should().BeFalse();
        user.RedeemEmailVerificationToken("new", Now).Should().BeTrue();
    }

    [Fact]
    public void Anonymize_scrubs_profile_and_tokens()
    {
        var user = Register(state: UserActivationState.PendingEmailVerification);
        user.IssueEmailVerificationToken("hash-1", Now.AddHours(24));

        user.Anonymize();

        user.Profile.FullName.Should().BeNull();
        user.EmailVerificationTokens.Should().BeEmpty();
        user.Email.Should().StartWith("erased-");
    }
}
