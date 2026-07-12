using System.Security.Cryptography;
using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

public class AppUserMfaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    private static OwnerUser NewUser()
        => OwnerUser.Create(new Email("mfa@test.local"), "hash", RandomNumberGenerator.GetBytes(32),
            mustChangePassword: false);

    private static byte[] Secret() => Encoding.UTF8.GetBytes("SECRET");

    [Fact]
    public void Begin_enrollment_stores_pending_secret_without_enabling()
    {
        var user = NewUser();

        user.BeginMfaEnrollment(Secret());

        user.MfaEnabled.Should().BeFalse();
        user.MfaEnrollmentPending.Should().BeTrue();
        user.EncryptedMfaSecret.Should().NotBeNull();
    }

    [Fact]
    public void Begin_enrollment_rejects_empty_secret()
    {
        var user = NewUser();
        var act = () => user.BeginMfaEnrollment([]);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MfaSecretRequired);
    }

    [Fact]
    public void Confirm_enables_mfa_and_stores_backup_codes()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(Secret());
        var hashes = MfaBackupCodes.Generate(10, 10).Select(MfaBackupCodes.Hash).ToArray();

        user.ConfirmMfaEnrollment(hashes);

        user.MfaEnabled.Should().BeTrue();
        user.MfaEnrollmentPending.Should().BeFalse();
        user.UnusedBackupCodeCount.Should().Be(10);
    }

    [Fact]
    public void Confirm_without_pending_enrollment_throws()
    {
        var user = NewUser();
        var act = () => user.ConfirmMfaEnrollment(["h"]);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MfaEnrollmentNotPending);
    }

    [Fact]
    public void Backup_code_is_single_use()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(Secret());
        var codes = MfaBackupCodes.Generate(3, 10);
        user.ConfirmMfaEnrollment([.. codes.Select(MfaBackupCodes.Hash)]);

        var hash = MfaBackupCodes.Hash(codes[0]);
        user.ConsumeBackupCode(hash, Now).Should().BeTrue();
        user.ConsumeBackupCode(hash, Now).Should().BeFalse();
        user.UnusedBackupCodeCount.Should().Be(2);
    }

    [Fact]
    public void Consume_unknown_backup_code_returns_false()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(Secret());
        user.ConfirmMfaEnrollment([MfaBackupCodes.Hash("AAAAABBBBB")]);

        user.ConsumeBackupCode(MfaBackupCodes.Hash("ZZZZZZZZZZ"), Now).Should().BeFalse();
    }

    [Fact]
    public void Regenerate_replaces_all_codes()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(Secret());
        var first = MfaBackupCodes.Generate(2, 10);
        user.ConfirmMfaEnrollment([.. first.Select(MfaBackupCodes.Hash)]);

        var second = MfaBackupCodes.Generate(4, 10);
        user.RegenerateBackupCodes([.. second.Select(MfaBackupCodes.Hash)]);

        user.UnusedBackupCodeCount.Should().Be(4);
        user.ConsumeBackupCode(MfaBackupCodes.Hash(first[0]), Now).Should().BeFalse();
        user.ConsumeBackupCode(MfaBackupCodes.Hash(second[0]), Now).Should().BeTrue();
    }

    [Fact]
    public void Regenerate_requires_mfa_enabled()
    {
        var user = NewUser();
        var act = () => user.RegenerateBackupCodes([MfaBackupCodes.Hash("AAAAABBBBB")]);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MfaNotEnabled);
    }

    [Fact]
    public void Disable_clears_secret_and_codes()
    {
        var user = NewUser();
        user.BeginMfaEnrollment(Secret());
        user.ConfirmMfaEnrollment([MfaBackupCodes.Hash("AAAAABBBBB")]);

        user.DisableMfa();

        user.MfaEnabled.Should().BeFalse();
        user.EncryptedMfaSecret.Should().BeNull();
        user.UnusedBackupCodeCount.Should().Be(0);
    }

    [Fact]
    public void Consume_when_disabled_throws()
    {
        var user = NewUser();
        var act = () => user.ConsumeBackupCode("h", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.MfaNotEnabled);
    }
}
