using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.OpenApi;

/// <summary>
/// Manages the deployment-wide shared Open API application: idempotent seed from config, owner runtime
/// create/update/remove, and the hard-block/remove reconcile that enforces shared-mode (personal apps are
/// removed and their authorizations re-pointed at the shared app, requiring re-authorization).
/// </summary>
public sealed class SharedOpenApiAppService(
    DataContext db,
    ISecretProtector protector,
    IOptionsMonitor<AppOptions> options,
    ILogger<SharedOpenApiAppService> log)
{
    public Task<OpenApiApplication?> GetSharedAsync(CancellationToken ct)
        => db.OpenApiApplications.FirstOrDefaultAsync(a => a.IsShared, ct);

    public string? RedirectUrlFromConfig()
    {
        var baseUrl = options.CurrentValue.OpenApi.PublicBaseUrl;
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : baseUrl.TrimEnd('/') + Core.Constants.OpenApiEndpoints.CallbackPath;
    }

    public async Task SeedFromConfigAsync(CancellationToken ct)
    {
        var existing = await GetSharedAsync(ct);
        if (existing is not null)
        {
            await ReconcileHardBlockAsync(existing, ct);
            return;
        }

        var cfg = options.CurrentValue.OpenApi.SharedApp;
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            return;

        var redirect = RedirectUrlFromConfig();
        if (redirect is null)
        {
            log.SharedOpenApiAppSeedSkipped("App:OpenApi:PublicBaseUrl is not set");
            return;
        }

        var owner = await db.Users.OfType<OwnerUser>().FirstOrDefaultAsync(ct);
        if (owner is null)
        {
            log.SharedOpenApiAppSeedSkipped("owner account not seeded");
            return;
        }

        var app = OpenApiApplication.CreateShared(owner.Id, cfg.Name, new OpenApiClientId(cfg.ClientId),
            protector.Protect(Encoding.UTF8.GetBytes(cfg.ClientSecret), EncryptionPurposes.OpenApiClientSecret),
            new OpenApiRedirectUri(redirect));
        db.OpenApiApplications.Add(app);
        await db.SaveChangesAsync(ct);
        log.SharedOpenApiAppConfigured(app.ClientId);
        await ReconcileHardBlockAsync(app, ct);
    }

    public async Task<OpenApiApplication> SaveOwnerSharedAsync(
        string name, OpenApiClientId clientId, string? clientSecret, OpenApiRedirectUri redirectUri, CancellationToken ct)
    {
        var existing = await GetSharedAsync(ct);
        if (existing is null)
        {
            if (string.IsNullOrEmpty(clientSecret)) throw new DomainException(DomainErrors.OpenApiSecretRequired);
            var owner = await db.Users.OfType<OwnerUser>().FirstAsync(ct);
            existing = OpenApiApplication.CreateShared(owner.Id, name, clientId,
                protector.Protect(Encoding.UTF8.GetBytes(clientSecret), EncryptionPurposes.OpenApiClientSecret),
                redirectUri);
            db.OpenApiApplications.Add(existing);
            await db.SaveChangesAsync(ct);
            log.SharedOpenApiAppConfigured(existing.ClientId);
        }
        else
        {
            var secret = string.IsNullOrEmpty(clientSecret)
                ? existing.EncryptedClientSecret
                : protector.Protect(Encoding.UTF8.GetBytes(clientSecret), EncryptionPurposes.OpenApiClientSecret);
            existing.UpdateCredentials(name, clientId, secret, redirectUri);
            await db.SaveChangesAsync(ct);
        }

        await ReconcileHardBlockAsync(existing, ct);
        return existing;
    }

    public async Task<bool> RemoveSharedAsync(CancellationToken ct)
    {
        var existing = await GetSharedAsync(ct);
        if (existing is null) return false;
        db.OpenApiApplications.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ReconcileHardBlockAsync(OpenApiApplication shared, CancellationToken ct)
    {
        // Re-point personal-app authorizations at the shared app so the accounts survive (tokens fail
        // refresh under the new client id and escalate until the user re-authorizes). Administrative
        // reconcile — a single save over these append-only-style records is acceptable here.
        var strays = await db.OpenApiAuthorizations.Where(a => a.ApplicationId != shared.Id).ToListAsync(ct);
        foreach (var authorization in strays)
            authorization.ReassignToApplication(shared.Id);
        if (strays.Count > 0)
            await db.SaveChangesAsync(ct);

        var personal = await db.OpenApiApplications.Where(a => !a.IsShared).ToListAsync(ct);
        if (personal.Count > 0)
        {
            db.OpenApiApplications.RemoveRange(personal);
            await db.SaveChangesAsync(ct);
            log.SharedOpenApiAppRemovedPersonal(personal.Count);
        }
    }
}
