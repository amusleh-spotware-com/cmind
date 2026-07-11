using System.Security.Cryptography;
using Core;
using Core.Constants;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class MigrationLockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Concurrent_migrate_and_seed_runs_once_and_seeds_a_single_owner()
    {
        var connectionString = fixture.Container.GetConnectionString();

        async Task MigrateAndSeedAsync()
        {
            await using var db = CreateContext();
            await MigrationLock.RunExclusiveAsync(connectionString, DatabaseDefaults.MigrationAdvisoryLockKey,
                async ct =>
                {
                    await db.Database.MigrateAsync(ct);
                    if (await db.Users.OfType<OwnerUser>().AnyAsync(ct)) return;
                    db.Users.Add(OwnerUser.Create(
                        new Email("owner@migration-lock.invalid"), "hash", RandomNumberGenerator.GetBytes(32)));
                    await db.SaveChangesAsync(ct);
                }, CancellationToken.None);
        }

        // Three replicas racing a fresh database: the advisory lock serializes them, so exactly one
        // migrates + seeds and the others no-op. Without it, the concurrent owner insert would throw on
        // the unique email index.
        var act = () => Task.WhenAll(MigrateAndSeedAsync(), MigrateAndSeedAsync(), MigrateAndSeedAsync());
        await act.Should().NotThrowAsync();

        await using var verify = CreateContext();
        (await verify.Users.OfType<OwnerUser>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Lock_is_released_when_guarded_work_throws()
    {
        var connectionString = fixture.Container.GetConnectionString();
        const long key = DatabaseDefaults.MigrationAdvisoryLockKey + 1;

        var faulting = () => MigrationLock.RunExclusiveAsync(connectionString, key,
            _ => throw new InvalidOperationException("boom"), CancellationToken.None);
        await faulting.Should().ThrowAsync<InvalidOperationException>();

        // If the lock leaked, this second acquisition would block forever; a short timeout proves release.
        var reacquire = MigrationLock.RunExclusiveAsync(connectionString, key,
            _ => Task.CompletedTask, CancellationToken.None);
        (await Task.WhenAny(reacquire, Task.Delay(TimeSpan.FromSeconds(10)))).Should().Be(reacquire);
        await reacquire;
    }
}
