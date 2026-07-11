using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class AuditChainIntegrityTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 07, 11, 12, 0, 0, TimeSpan.Zero);

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System), new AuditChainInterceptor())
            .Options);

    [Fact]
    public async Task Chain_verifies_intact_then_detects_tampering()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        for (var i = 0; i < 3; i++)
        {
            await using var write = CreateContext();
            write.AuditLogs.Add(AuditLog.Record($"Action{i}", "Test", Now.AddSeconds(i)));
            await write.SaveChangesAsync();
        }

        await using (var verify = CreateContext())
        {
            var status = await new AuditTrailVerifier(verify).VerifyAsync(CancellationToken.None);
            status.Intact.Should().BeTrue();
            status.Count.Should().BeGreaterThanOrEqualTo(3);
        }

        await using (var tamper = CreateContext())
        {
            await tamper.Database.ExecuteSqlRawAsync(
                "UPDATE \"AuditLogs\" SET \"Action\" = 'HACKED' WHERE \"Action\" = 'Action1'");
        }

        await using (var verify = CreateContext())
        {
            var status = await new AuditTrailVerifier(verify).VerifyAsync(CancellationToken.None);
            status.Intact.Should().BeFalse();
            status.FirstBrokenId.Should().NotBeNull();
        }
    }
}
