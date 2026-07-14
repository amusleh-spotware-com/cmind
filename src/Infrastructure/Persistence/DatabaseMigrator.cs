using Core.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations under the shared advisory lock. Call this synchronously at startup — after
/// <c>builder.Build()</c> and BEFORE the host starts serving or runs background services — so nothing
/// (settings readers, the DataProtection keyring, node pollers) ever queries a not-yet-created schema.
/// Idempotent and cross-process safe via <see cref="MigrationLock"/>: whichever replica wins migrates, the
/// rest block then see a no-op. Seeding stays in the Web host (it needs Web services); this is migrate-only.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Missing database connection string.");

        await MigrationLock.RunExclusiveAsync(connectionString, DatabaseDefaults.MigrationAdvisoryLockKey,
            token => db.Database.MigrateAsync(token), ct);
    }
}
