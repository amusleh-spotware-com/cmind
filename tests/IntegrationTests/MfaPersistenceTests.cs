using System.Text;
using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class MfaPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Enrollment_confirmation_and_backup_code_consumption_round_trip()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"mfa-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var codes = MfaBackupCodes.Generate(10, 10);
        user.BeginMfaEnrollment(Encoding.UTF8.GetBytes("encrypted-secret"));
        user.ConfirmMfaEnrollment([.. codes.Select(MfaBackupCodes.Hash)]);

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var loaded = await read.Users.Include(u => u.BackupCodes).FirstAsync(u => u.Id == user.Id);
            loaded.MfaEnabled.Should().BeTrue();
            loaded.EncryptedMfaSecret.Should().NotBeNull();
            loaded.UnusedBackupCodeCount.Should().Be(10);
        }

        // Redeem one recovery code and persist the burn.
        await using (var consume = CreateContext())
        {
            var loaded = await consume.Users.Include(u => u.BackupCodes).FirstAsync(u => u.Id == user.Id);
            loaded.ConsumeBackupCode(MfaBackupCodes.Hash(codes[0]), TestClock.Now).Should().BeTrue();
            await consume.SaveChangesAsync();
        }

        await using (var after = CreateContext())
        {
            var loaded = await after.Users.Include(u => u.BackupCodes).FirstAsync(u => u.Id == user.Id);
            loaded.UnusedBackupCodeCount.Should().Be(9);
            // The burned code is not accepted a second time.
            loaded.ConsumeBackupCode(MfaBackupCodes.Hash(codes[0]), TestClock.Now).Should().BeFalse();
        }
    }

    [Fact]
    public async Task Disable_deletes_backup_codes()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"mfa-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        user.BeginMfaEnrollment(Encoding.UTF8.GetBytes("secret"));
        user.ConfirmMfaEnrollment([.. MfaBackupCodes.Generate(5, 10).Select(MfaBackupCodes.Hash)]);

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            await write.SaveChangesAsync();
        }

        await using (var disable = CreateContext())
        {
            var loaded = await disable.Users.Include(u => u.BackupCodes).FirstAsync(u => u.Id == user.Id);
            loaded.DisableMfa();
            await disable.SaveChangesAsync();
        }

        await using var after = CreateContext();
        var final = await after.Users.Include(u => u.BackupCodes).FirstAsync(u => u.Id == user.Id);
        final.MfaEnabled.Should().BeFalse();
        final.EncryptedMfaSecret.Should().BeNull();
        final.BackupCodes.Should().BeEmpty();
    }
}
