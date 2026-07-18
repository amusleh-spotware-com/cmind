using Core;
using Core.Ai;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

// The AiTaskRunner claim/lease/lifecycle exercised without Docker by using a feature the worker does not run
// as a task (ReviewCBot) — it still claims, dispatches, and fails cleanly, proving the FOR UPDATE SKIP LOCKED
// claim, the self-healing lease reclaim, and terminal persistence. The real build path is covered by the
// AiBuild E2E (which now shares CBotBuildFlow).
public class AiTaskRunnerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private AiTaskRunner BuildRunner()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(o => o
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)));
        var sp = services.BuildServiceProvider();
        return new AiTaskRunner(sp.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            new StaticOptionsMonitor<AppOptions>(new AppOptions()), NullLogger<AiTaskRunner>.Instance);
    }

    private static async Task<UserId> SeedUserAsync(DataContext db)
    {
        var user = OwnerUser.Create(new Email($"tasks-{Guid.NewGuid():N}@test.local"), "hash", Guid.NewGuid().ToByteArray());
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.AiTasks.IgnoreQueryFilters().ExecuteDeleteAsync();
        return db;
    }

    private static AiTask NewTask(UserId uid, DateTimeOffset now) =>
        AiTask.Create(uid, AiFeature.ReviewCBot, AiProviderCredentialId.New(), """{"description":"x"}""", now);

    [Fact]
    public async Task Claims_a_queued_task_and_fails_an_unsupported_feature_cleanly()
    {
        await using var db = await FreshAsync();
        var uid = await SeedUserAsync(db);
        var task = NewTask(uid, TimeProvider.System.GetUtcNow());
        db.AiTasks.Add(task);
        await db.SaveChangesAsync();
        var id = task.Id;

        (await BuildRunner().RunOnceAsync(CancellationToken.None)).Should().BeTrue();

        await using var check = CreateContext();
        var reloaded = await check.AiTasks.FirstAsync(t => t.Id == id);
        reloaded.Status.Should().Be(AiTaskStatus.Failed);
        reloaded.Error.Should().Contain("background task");
        reloaded.ClaimedBy.Should().BeNull();
        reloaded.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Processes_one_task_per_cycle_leaving_the_rest_queued()
    {
        await using var db = await FreshAsync();
        var uid = await SeedUserAsync(db);
        var now = TimeProvider.System.GetUtcNow();
        db.AiTasks.Add(NewTask(uid, now.AddMinutes(-2)));
        db.AiTasks.Add(NewTask(uid, now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        (await BuildRunner().RunOnceAsync(CancellationToken.None)).Should().BeTrue();

        await using var check = CreateContext();
        (await check.AiTasks.CountAsync(t => t.Status == AiTaskStatus.Failed)).Should().Be(1);
        (await check.AiTasks.CountAsync(t => t.Status == AiTaskStatus.Queued)).Should().Be(1);
    }

    [Fact]
    public async Task Reclaims_an_orphaned_running_task_whose_lease_expired()
    {
        await using var db = await FreshAsync();
        var uid = await SeedUserAsync(db);
        var past = TimeProvider.System.GetUtcNow().AddMinutes(-30);
        var task = NewTask(uid, past);
        task.Claim("dead-node", past, Lease); // lease expired at past + 15m (~15m ago)
        db.AiTasks.Add(task);
        await db.SaveChangesAsync();
        var id = task.Id;

        (await BuildRunner().RunOnceAsync(CancellationToken.None)).Should().BeTrue();

        await using var check = CreateContext();
        (await check.AiTasks.FirstAsync(t => t.Id == id)).Status.Should().Be(AiTaskStatus.Failed);
    }

    [Fact]
    public async Task Does_not_claim_a_running_task_with_a_live_lease()
    {
        await using var db = await FreshAsync();
        var uid = await SeedUserAsync(db);
        var now = TimeProvider.System.GetUtcNow();
        var task = NewTask(uid, now);
        task.Claim("busy-node", now, Lease); // lease valid for 15m
        db.AiTasks.Add(task);
        await db.SaveChangesAsync();
        var id = task.Id;

        (await BuildRunner().RunOnceAsync(CancellationToken.None)).Should().BeFalse();

        await using var check = CreateContext();
        (await check.AiTasks.FirstAsync(t => t.Id == id)).Status.Should().Be(AiTaskStatus.Running);
    }
}
