using System.Security.Cryptography;
using Core;
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
    IOptionsMonitor<CtwOptions> options,
    ILogger<OwnerSeeder> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CtwDbContext>();
        await db.Database.MigrateAsync(ct);

        if (await db.Users.OfType<OwnerUser>().AnyAsync(ct)) return;

        var opts = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.OwnerEmail) || string.IsNullOrWhiteSpace(opts.OwnerPassword))
        {
            log.OwnerCredentialsMissing();
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var email = new Email(opts.OwnerEmail);
        var owner = new OwnerUser
        {
            Email = email.Value,
            NormalizedEmail = email.Normalized,
            PasswordHash = hasher.Hash(opts.OwnerPassword),
            SecurityStamp = RandomNumberGenerator.GetBytes(32),
            MustChangePassword = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync(ct);
        log.OwnerSeeded(email.Value);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
