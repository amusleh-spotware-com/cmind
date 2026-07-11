using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Serializes work that must run on exactly one replica at a time — schema migration and first-run
/// seeding — behind a Postgres session-level advisory lock held on a dedicated connection. Other
/// replicas block on <c>pg_advisory_lock</c> until the holder releases, after which their migration is
/// a no-op and their seed sees the already-created rows. Prevents the concurrent-migration race on a
/// rolling deploy or scale-out.
/// </summary>
public static class MigrationLock
{
    public static async Task RunExclusiveAsync(
        string connectionString, long lockKey, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using (var acquire = connection.CreateCommand())
        {
            acquire.CommandText = "SELECT pg_advisory_lock(@key)";
            acquire.Parameters.AddWithValue("key", lockKey);
            await acquire.ExecuteNonQueryAsync(ct);
        }

        try
        {
            await work(ct);
        }
        finally
        {
            await using var release = connection.CreateCommand();
            release.CommandText = "SELECT pg_advisory_unlock(@key)";
            release.Parameters.AddWithValue("key", lockKey);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
