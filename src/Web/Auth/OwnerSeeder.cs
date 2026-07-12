using System.Security.Cryptography;
using Core;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Auth;

public sealed class OwnerSeeder(
    IServiceScopeFactory sf,
    IOptionsMonitor<AppOptions> options,
    ILogger<OwnerSeeder> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Missing database connection string.");

        // Migrate + seed under one advisory lock so a rolling deploy / scale-out never runs migrations
        // concurrently and never double-seeds the owner (the AnyAsync check is only safe single-writer).
        await MigrationLock.RunExclusiveAsync(connectionString, DatabaseDefaults.MigrationAdvisoryLockKey,
            async token =>
            {
                await db.Database.MigrateAsync(token);
                await SeedOwnerAsync(scope, db, token);
                await scope.ServiceProvider.GetRequiredService<Core.Ai.IAiProviderStore>()
                    .SeedFromConfigAsync(token);
            }, ct);
    }

    private async Task SeedOwnerAsync(IServiceScope scope, DataContext db, CancellationToken ct)
    {
        if (await db.Users.OfType<OwnerUser>().AnyAsync(ct)) return;

        var opts = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.OwnerEmail) || string.IsNullOrWhiteSpace(opts.OwnerPassword))
        {
            log.OwnerCredentialsMissing();
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var email = new Email(opts.OwnerEmail);
        var owner = OwnerUser.Create(email, hasher.Hash(opts.OwnerPassword), RandomNumberGenerator.GetBytes(32));
        db.Users.Add(owner);
        await db.SaveChangesAsync(ct);
        log.OwnerSeeded(email.Value);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
