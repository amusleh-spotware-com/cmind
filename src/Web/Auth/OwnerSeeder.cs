using System.Security.Cryptography;
using Core;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Auth;

// Applies migrations AND seeds first-run data (owner user, config-seeded AI providers, shared Open API
// app) under one advisory lock. Invoked synchronously from Program.cs after Build() and BEFORE app.Run(),
// so the schema exists before any background service, request, DataProtection keyring read, or settings
// lookup touches the database — otherwise a fresh DB logs a burst of "relation does not exist" errors
// until the migration eventually lands.
public sealed class OwnerSeeder(
    IServiceScopeFactory sf,
    IOptionsMonitor<AppOptions> options,
    ILogger<OwnerSeeder> log)
{
    public async Task InitializeAsync(CancellationToken ct)
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
                await scope.ServiceProvider.GetRequiredService<Infrastructure.OpenApi.SharedOpenApiAppService>()
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
        // The owner password is explicit deployment config (App:OwnerPassword), not a generated temp
        // password — the operator chose it, so don't force a change-password on first sign-in (which the
        // MustChangePassword enforcement guard would otherwise require).
        var owner = OwnerUser.Create(email, hasher.Hash(opts.OwnerPassword), RandomNumberGenerator.GetBytes(32),
            mustChangePassword: false);
        db.Users.Add(owner);
        await db.SaveChangesAsync(ct);
        log.OwnerSeeded(email.Value);
    }
}
