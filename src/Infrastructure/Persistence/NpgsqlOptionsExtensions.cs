using Core.Constants;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Uniform Npgsql configuration for hosts that register <see cref="DataContext"/> directly (not via the
/// Aspire component): a retrying execution strategy so a transient disconnect / managed-Postgres failover
/// is retried instead of surfacing as an error, plus an explicit command timeout.
/// </summary>
public static class NpgsqlOptionsExtensions
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(DatabaseDefaults.MaxRetryDelaySeconds);

    public static DbContextOptionsBuilder UseAppNpgsql(
        this DbContextOptionsBuilder builder, string connectionString)
        => builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.EnableRetryOnFailure(DatabaseDefaults.MaxRetryCount, MaxRetryDelay, errorCodesToAdd: null);
            npgsql.CommandTimeout(DatabaseDefaults.CommandTimeoutSeconds);
        });
}
