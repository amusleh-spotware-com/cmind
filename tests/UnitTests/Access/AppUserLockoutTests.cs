using System.Security.Cryptography;
using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

// Invariants + transitions for the AppUser auth lifecycle: failed-login lockout, successful-login reset,
// password change/reset, profile update, and the email-verification-token guard. (WS-1 Core backfill.)
public class AppUserLockoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private static OwnerUser NewUser(bool mustChangePassword = false)
        => OwnerUser.Create(new Email("lockout@test.local"), "hash-1", RandomNumberGenerator.GetBytes(32),
            mustChangePassword);

    [Fact]
    public void Failed_login_below_threshold_does_not_lock()
    {
        var user = NewUser();

        var locked = user.RecordFailedLogin(maxAttempts: 3, Lockout, Now);

        locked.Should().BeFalse();
        user.AccessFailedCount.Should().Be(1);
        user.LockoutEnd.Should().BeNull();
        user.IsCurrentlyLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void Reaching_the_threshold_locks_and_resets_the_counter()
    {
        var user = NewUser();

        user.RecordFailedLogin(3, Lockout, Now).Should().BeFalse();
        user.RecordFailedLogin(3, Lockout, Now).Should().BeFalse();
        var locked = user.RecordFailedLogin(3, Lockout, Now);

        locked.Should().BeTrue();
        user.LockoutEnd.Should().Be(Now.Add(Lockout));
        user.AccessFailedCount.Should().Be(0, "the counter resets once the lockout window opens");
        user.IsCurrentlyLockedOut(Now).Should().BeTrue();
        user.IsCurrentlyLockedOut(Now.Add(Lockout).AddSeconds(1)).Should().BeFalse("the lock expires with time");
    }

    [Fact]
    public void Successful_login_clears_failures_and_lockout()
    {
        var user = NewUser();
        user.RecordFailedLogin(3, Lockout, Now);
        user.RecordFailedLogin(3, Lockout, Now);

        user.RecordSuccessfulLogin();

        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
        user.IsCurrentlyLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void Change_password_updates_hash_and_clears_the_must_change_flag()
    {
        var user = NewUser(mustChangePassword: true);
        user.MustChangePassword.Should().BeTrue();

        user.ChangePassword("hash-2");

        user.PasswordHash.Should().Be("hash-2");
        user.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void Change_password_rejects_blank()
    {
        var user = NewUser();
        var act = () => user.ChangePassword("   ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Reset_password_rotates_stamp_forces_change_and_unlocks()
    {
        var user = NewUser();
        user.RecordFailedLogin(3, Lockout, Now); // build some failed state to prove it clears
        var newStamp = RandomNumberGenerator.GetBytes(32);

        user.ResetPassword("hash-reset", newStamp);

        user.PasswordHash.Should().Be("hash-reset");
        user.SecurityStamp.Should().BeEquivalentTo(newStamp);
        user.MustChangePassword.Should().BeTrue();
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
        user.IsCurrentlyLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void Update_profile_with_null_falls_back_to_empty()
    {
        var user = NewUser();

        user.UpdateProfile(null!);

        user.Profile.Should().Be(UserProfile.Empty);
    }

    [Fact]
    public void Issuing_an_email_verification_token_rejects_a_blank_hash()
    {
        var user = NewUser();
        var act = () => user.IssueEmailVerificationToken("  ", Now.AddHours(1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.EmailVerificationTokenRequired);
    }

    [Fact]
    public void Confirming_mfa_enrollment_requires_backup_codes()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(RandomNumberGenerator.GetBytes(16));

        var act = () => user.ConfirmMfaEnrollment([]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MfaBackupCodesRequired);
    }

    [Fact]
    public void Regenerating_backup_codes_requires_some_codes()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(RandomNumberGenerator.GetBytes(16));
        user.ConfirmMfaEnrollment(["h1", "h2"]);

        var act = () => user.RegenerateBackupCodes([]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MfaBackupCodesRequired);
    }
}
