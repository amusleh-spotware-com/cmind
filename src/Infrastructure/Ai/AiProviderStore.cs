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

/// <summary>
/// Owner-managed store of AI provider credentials. Supersedes the single-key <c>AiKeyStore</c>: N
/// providers may be stored, exactly one active. Keys are persisted encrypted via
/// <see cref="ISecretProtector"/>; the active provider is resolved (key decrypted) and cached with a
/// short TTL so gating stays cheap. Back-compat: when no rows exist but a legacy <c>ai.api_key</c>
/// AppSetting or <c>App:Ai:ApiKey</c> config is present, a default active Anthropic provider is
/// synthesized in memory, so every existing deployment keeps working with zero action.
/// </summary>
public sealed class AiProviderStore(
    IOptionsMonitor<AppOptions> options,
    DataContext db,
    IMemoryCache cache,
    ISecretProtector protector,
    TimeProvider timeProvider) : IAiProviderStore
{
    public bool HasActive => Active is not null;

    public ActiveAiProvider? Active
    {
        get
        {
            if (cache.TryGetValue(AiConstants.ActiveProviderCacheKey, out ActiveAiProvider? cached))
                return cached;

            var resolved = Resolve();
            cache.Set(AiConstants.ActiveProviderCacheKey, resolved, AiConstants.ActiveProviderCacheTtl);
            return resolved;
        }
    }

    private ActiveAiProvider? Resolve()
    {
        var active = db.Set<AiProviderCredential>().AsNoTracking().FirstOrDefault(c => c.IsActive);
        if (active is not null)
            return new ActiveAiProvider(active.Kind, active.BaseUrl, active.Model,
                Decrypt(active.EncryptedApiKey), active.Capabilities, active.MaxTokens);

        // No stored credentials — honour the legacy single-key config as a default Anthropic provider.
        var legacyKey = LegacyKey();
        if (string.IsNullOrWhiteSpace(legacyKey)) return null;

        var ai = options.CurrentValue.Ai;
        return new ActiveAiProvider(AiProviderKind.Anthropic, ai.BaseUrl, ai.Model, legacyKey,
            AiProviderCapabilities.DefaultFor(AiProviderKind.Anthropic), ai.MaxTokens);
    }

    public async Task<IReadOnlyList<AiProviderView>> ListAsync(CancellationToken ct)
    {
        var rows = await db.Set<AiProviderCredential>().AsNoTracking()
            .OrderByDescending(c => c.IsActive).ThenByDescending(c => c.CreatedAt).ToListAsync(ct);
        return rows.Select(c => new AiProviderView(
            c.Id.Value, c.Kind, c.BaseUrl, c.Model, c.HasKey, c.IsActive, c.MaxTokens, c.Capabilities)).ToList();
    }

    public async Task<Guid> UpsertAsync(UpsertAiProviderCommand command, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var baseUrl = new AiEndpoint(command.BaseUrl);
        AiProviderPolicy.EnsureAllowed(command.Kind, baseUrl, options.CurrentValue.Branding);
        var model = new AiModelId(command.Model);
        var maxTokens = command.MaxTokens ?? AiConstants.DefaultMaxTokens;
        var caps = command.Capabilities ?? AiProviderCapabilities.DefaultFor(command.Kind);
        var encrypted = string.IsNullOrWhiteSpace(command.ApiKey) ? null : Encrypt(command.ApiKey!);

        AiProviderCredential credential;
        if (command.Id is { } id)
        {
            var cid = AiProviderCredentialId.From(id);
            credential = await db.Set<AiProviderCredential>().FirstOrDefaultAsync(c => c.Id == cid, ct)
                ?? throw new InvalidOperationException("Provider credential not found.");
            credential.Retarget(baseUrl, model, maxTokens, now);
            credential.OverrideCapabilities(caps, now);
            // Only overwrite the stored key when a new one is supplied (blank = keep existing).
            if (!string.IsNullOrWhiteSpace(command.ApiKey)) credential.Rotate(encrypted, now);
        }
        else
        {
            credential = AiProviderCredential.Create(command.Kind, baseUrl, model, encrypted, caps, maxTokens, now);
            db.Set<AiProviderCredential>().Add(credential);
        }

        if (command.Activate) await DeactivateOthersAndActivateAsync(credential, now, ct);

        await db.SaveChangesAsync(ct);
        Invalidate();
        return credential.Id.Value;
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct)
    {
        var cid = AiProviderCredentialId.From(id);
        var credential = await db.Set<AiProviderCredential>().FirstOrDefaultAsync(c => c.Id == cid, ct)
            ?? throw new InvalidOperationException("Provider credential not found.");
        await DeactivateOthersAndActivateAsync(credential, timeProvider.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        var cid = AiProviderCredentialId.From(id);
        var credential = await db.Set<AiProviderCredential>().FirstOrDefaultAsync(c => c.Id == cid, ct);
        if (credential is null) return;
        // Soft delete (base is ISoftDeletable) — clear active first so the partial-unique active index
        // never counts a removed row.
        credential.Deactivate(timeProvider.GetUtcNow());
        db.Set<AiProviderCredential>().Remove(credential);
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task SeedFromConfigAsync(CancellationToken ct)
    {
        if (await db.Set<AiProviderCredential>().AnyAsync(ct)) return;

        var app = options.CurrentValue;
        if (app.Ai.Providers.Count == 0)
        {
            // No configured providers — fall back to the shipped built-in local LLM so every deployment
            // has working AI out of the box (unless the white-label deployment removed it).
            await SeedBuiltInAsync(app, ct);
            return;
        }

        var ai = app.Ai;
        var now = timeProvider.GetUtcNow();
        var activeKind = ai.ActiveProvider;
        var madeActive = false;
        foreach (var p in ai.Providers)
        {
            var baseUrl = new AiEndpoint(p.BaseUrl ?? DefaultBaseUrlFor(p.Kind));
            var model = new AiModelId(p.Model ?? DefaultModelFor(p.Kind));
            var caps = MergeCapabilities(p.Kind, p.Capabilities);
            var encrypted = string.IsNullOrWhiteSpace(p.ApiKey) ? null : Encrypt(p.ApiKey!);
            var credential = AiProviderCredential.Create(
                p.Kind, baseUrl, model, encrypted, caps, p.MaxTokens ?? AiConstants.DefaultMaxTokens, now);

            // Activate the configured ActiveProvider kind, else the first seeded provider.
            if (!madeActive && (activeKind == p.Kind || activeKind is null))
            {
                credential.Activate(now);
                madeActive = true;
            }
            db.Set<AiProviderCredential>().Add(credential);
        }

        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    private async Task SeedBuiltInAsync(AppOptions app, CancellationToken ct)
    {
        if (!app.Branding.AllowBuiltInAi || !app.Ai.BuiltIn.Enabled) return;

        var now = timeProvider.GetUtcNow();
        var credential = AiProviderCredential.Create(
            AiProviderKind.BuiltInOnnx,
            new AiEndpoint(AiConstants.BuiltInBaseUrl),
            new AiModelId(AiConstants.BuiltInModel),
            null,
            AiProviderCapabilities.DefaultFor(AiProviderKind.BuiltInOnnx),
            app.Ai.BuiltIn.MaxTokens,
            now);
        credential.Activate(now);
        db.Set<AiProviderCredential>().Add(credential);
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    private async Task DeactivateOthersAndActivateAsync(AiProviderCredential target, DateTimeOffset now, CancellationToken ct)
    {
        var others = await db.Set<AiProviderCredential>().Where(c => c.IsActive && c.Id != target.Id).ToListAsync(ct);
        foreach (var other in others) other.Deactivate(now);
        target.Activate(now);
    }

    private void Invalidate() => cache.Remove(AiConstants.ActiveProviderCacheKey);

    private string? LegacyKey()
    {
        var stored = db.AppSettings.AsNoTracking()
            .Where(s => s.Key == AiConstants.ApiKeySettingKey).Select(s => s.Value).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(stored))
        {
            var decoded = TryDecryptBase64(stored);
            if (!string.IsNullOrWhiteSpace(decoded)) return decoded;
        }
        var configured = options.CurrentValue.Ai.ApiKey;
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    private byte[] Encrypt(string plaintext) =>
        protector.Protect(Encoding.UTF8.GetBytes(plaintext.Trim()), EncryptionPurposes.AiApiKey);

    private string? Decrypt(byte[]? encrypted)
    {
        if (encrypted is not { Length: > 0 }) return null;
        try { return Encoding.UTF8.GetString(protector.Unprotect(encrypted, EncryptionPurposes.AiApiKey)); }
        catch { return null; }
    }

    private string? TryDecryptBase64(string base64)
    {
        try { return Decrypt(Convert.FromBase64String(base64)); }
        catch { return null; }
    }

    private static string DefaultBaseUrlFor(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Anthropic => AiConstants.DefaultBaseUrl,
        AiProviderKind.OpenAiCompatible => OpenAiConstants.DefaultBaseUrl,
        AiProviderKind.AzureOpenAi => OpenAiConstants.DefaultBaseUrl,
        AiProviderKind.Gemini => GeminiConstants.DefaultBaseUrl,
        AiProviderKind.Demo => AiConstants.DemoBaseUrl,
        AiProviderKind.BuiltInOnnx => AiConstants.BuiltInBaseUrl,
        _ => OpenAiConstants.DefaultBaseUrl
    };

    private static string DefaultModelFor(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Anthropic => AiConstants.DefaultModel,
        AiProviderKind.Demo => AiConstants.DemoModel,
        AiProviderKind.BuiltInOnnx => AiConstants.BuiltInModel,
        _ => "gpt-4o"
    };

    private static AiProviderCapabilities MergeCapabilities(AiProviderKind kind, AiCapabilityOptions? overrides)
    {
        var d = AiProviderCapabilities.DefaultFor(kind);
        if (overrides is null) return d;
        return new AiProviderCapabilities(
            overrides.SupportsWebSearch ?? d.SupportsWebSearch,
            overrides.SupportsVision ?? d.SupportsVision,
            overrides.SupportsSystemRole ?? d.SupportsSystemRole,
            overrides.SupportsTools ?? d.SupportsTools);
    }
}
