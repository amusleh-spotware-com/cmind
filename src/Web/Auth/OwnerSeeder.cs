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

public sealed class OwnerSeeder : IHostedService
{
    private readonly IServiceScopeFactory _sf;
    private readonly IOptionsMonitor<CtwOptions> _options;
    private readonly ILogger<OwnerSeeder> _log;

    public OwnerSeeder(IServiceScopeFactory sf, IOptionsMonitor<CtwOptions> options, ILogger<OwnerSeeder> log)
    {
        _sf = sf;
        _options = options;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sf.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CtwDbContext>();
        await db.Database.MigrateAsync(ct);

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Owner, ct)) return;

        var opts = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.OwnerEmail) || string.IsNullOrWhiteSpace(opts.OwnerPassword))
        {
            _log.OwnerCredentialsMissing();
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var email = new Email(opts.OwnerEmail);
        var owner = new AppUser
        {
            Email = email.Value,
            NormalizedEmail = email.Normalized,
            PasswordHash = hasher.Hash(opts.OwnerPassword),
            Role = UserRole.Owner,
            SecurityStamp = RandomNumberGenerator.GetBytes(32),
            MustChangePassword = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync(ct);
        _log.OwnerSeeded(email.Value);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
