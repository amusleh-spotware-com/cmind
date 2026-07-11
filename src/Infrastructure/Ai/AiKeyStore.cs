using System.Text;
using Core;
using Core.Ai;
using Core.Constants;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

public sealed class AiKeyStore(
    IOptionsMonitor<AppOptions> options,
    DataContext db,
    IMemoryCache cache,
    ISecretProtector protector,
    TimeProvider timeProvider) : IAiKeyStore
{
    public string? CurrentKey
    {
        get
        {
            var stored = LoadStoredKey();
            if (!string.IsNullOrWhiteSpace(stored)) return stored;
            var configured = options.CurrentValue.Ai.ApiKey;
            return string.IsNullOrWhiteSpace(configured) ? null : configured;
        }
    }

    public bool HasKey => !string.IsNullOrWhiteSpace(CurrentKey);

    public bool HasStoredKey => !string.IsNullOrWhiteSpace(LoadStoredKey());

    public async Task SetKeyAsync(string apiKey, CancellationToken ct)
    {
        var trimmed = apiKey.Trim();
        var encrypted = Convert.ToBase64String(
            protector.Protect(Encoding.UTF8.GetBytes(trimmed), EncryptionPurposes.AiApiKey));

        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == AiConstants.ApiKeySettingKey, ct);
        if (setting is null)
            db.AppSettings.Add(AppSetting.Create(AiConstants.ApiKeySettingKey, encrypted, timeProvider.GetUtcNow()));
        else
            setting.SetValue(encrypted, timeProvider.GetUtcNow());

        await db.SaveChangesAsync(ct);
        cache.Remove(AiConstants.ApiKeyCacheKey);
    }

    public async Task ClearKeyAsync(CancellationToken ct)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == AiConstants.ApiKeySettingKey, ct);
        if (setting is not null)
        {
            db.AppSettings.Remove(setting);
            await db.SaveChangesAsync(ct);
        }
        cache.Remove(AiConstants.ApiKeyCacheKey);
    }

    // Mirrors FeatureGate: the scoped DataContext is only touched inside the request scope and the cached
    // value is a plain string (or null), safe to share across the short TTL. A cached null means "checked,
    // no stored key" so we do not hit the DB on every gating call.
    private string? LoadStoredKey()
    {
        if (cache.TryGetValue(AiConstants.ApiKeyCacheKey, out string? cached))
            return cached;

        var encrypted = db.AppSettings.AsNoTracking()
            .Where(s => s.Key == AiConstants.ApiKeySettingKey)
            .Select(s => s.Value)
            .FirstOrDefault();

        string? key = null;
        if (!string.IsNullOrWhiteSpace(encrypted))
        {
            try
            {
                key = Encoding.UTF8.GetString(
                    protector.Unprotect(Convert.FromBase64String(encrypted), EncryptionPurposes.AiApiKey));
            }
            catch
            {
                key = null;
            }
        }

        cache.Set(AiConstants.ApiKeyCacheKey, key, AiConstants.ApiKeyCacheTtl);
        return key;
    }
}
