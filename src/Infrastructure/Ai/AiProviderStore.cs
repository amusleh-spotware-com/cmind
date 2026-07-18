using System.Text;
using Core;
using Core.Ai;
using Core.Constants;
using Core.Domain;
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
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAiProviderStore
{
    public bool HasActive => Active is not null;

    public ActiveAiProvider? Active
    {
        get
        {
            var user = currentUser.UserId;
            var cacheKey = CacheKey(user);
            if (cache.TryGetValue(cacheKey, out ActiveAiProvider? cached))
                return cached;

            var resolved = Resolve(user);
            cache.Set(cacheKey, resolved, AiConstants.ActiveProviderCacheTtl);
            return resolved;
        }
    }

    // Resolution order: the current user's own active credential, then the deployment default (the
    // white-label shared provider), then the legacy single-key config. So a deployment key powers every
    // user with no setup, while a user who adds their own provider overrides it for themselves.
    private ActiveAiProvider? Resolve(UserId? user)
    {
        if (user is { } uid)
        {
            var mine = db.Set<AiProviderCredential>().AsNoTracking()
                .FirstOrDefault(c => c.OwnerUserId == uid && c.IsActive);
            if (mine is not null) return ToActive(mine);
        }

        var deployment = db.Set<AiProviderCredential>().AsNoTracking()
            .FirstOrDefault(c => c.OwnerUserId == null && c.IsActive);
        if (deployment is not null) return ToActive(deployment);

        // No stored credentials — honour the legacy single-key config as a default Anthropic provider.
        var legacyKey = LegacyKey();
        if (string.IsNullOrWhiteSpace(legacyKey)) return null;

        var ai = options.CurrentValue.Ai;
        return new ActiveAiProvider(AiProviderKind.Anthropic, ai.BaseUrl, ai.Model, legacyKey,
            AiProviderCapabilities.DefaultFor(AiProviderKind.Anthropic), ai.MaxTokens);
    }

    private ActiveAiProvider ToActive(AiProviderCredential c) =>
        new(c.Kind, c.BaseUrl, c.Model, Decrypt(c.EncryptedApiKey), c.Capabilities, c.MaxTokens);

    public ActiveAiProvider? ResolveFor(AiFeature? feature, AiProviderCredentialId? credentialId)
    {
        var user = currentUser.UserId;
        if (credentialId is { } cid && LoadCredential(cid, user) is { } byId) return byId;
        if (feature is { } f && ResolveByFeature(f, user) is { } byFeature) return byFeature;
        return Active;
    }

    private ActiveAiProvider? ResolveByFeature(AiFeature feature, UserId? user)
    {
        if (user is { } uid)
        {
            var mine = db.Set<AiFeatureBinding>().AsNoTracking()
                .FirstOrDefault(b => b.OwnerUserId == uid && b.Feature == feature);
            if (mine is not null && LoadCredential(mine.CredentialId, uid) is { } p) return p;
        }

        var deployment = db.Set<AiFeatureBinding>().AsNoTracking()
            .FirstOrDefault(b => b.OwnerUserId == null && b.Feature == feature);
        if (deployment is not null && LoadCredential(deployment.CredentialId, null) is { } dp) return dp;

        return null;
    }

    // Load a credential the given scope may use: a user's own or the deployment default; deployment scope
    // (user == null) sees only deployment credentials.
    private ActiveAiProvider? LoadCredential(AiProviderCredentialId credentialId, UserId? owner)
    {
        var query = db.Set<AiProviderCredential>().AsNoTracking().Where(x => x.Id == credentialId);
        var credential = owner is { } uid
            ? query.FirstOrDefault(x => x.OwnerUserId == uid || x.OwnerUserId == null)
            : query.FirstOrDefault(x => x.OwnerUserId == null);
        return credential is null ? null : ToActive(credential);
    }

    public async Task<IReadOnlyList<AiFeatureBindingView>> ListBindingsAsync(UserId? owner, CancellationToken ct)
    {
        var query = db.Set<AiFeatureBinding>().AsNoTracking();
        var rows = owner is { } uid
            ? await query.Where(b => b.OwnerUserId == uid).ToListAsync(ct)
            : await query.Where(b => b.OwnerUserId == null).ToListAsync(ct);
        return rows.Select(b => new AiFeatureBindingView(b.Feature, b.CredentialId.Value)).ToList();
    }

    public async Task SetBindingAsync(UserId? owner, AiFeature feature, AiProviderCredentialId credentialId, CancellationToken ct)
    {
        var usable = owner is { } uid
            ? await db.Set<AiProviderCredential>()
                .AnyAsync(c => c.Id == credentialId && (c.OwnerUserId == uid || c.OwnerUserId == null), ct)
            : await db.Set<AiProviderCredential>()
                .AnyAsync(c => c.Id == credentialId && c.OwnerUserId == null, ct);
        if (!usable) throw new InvalidOperationException("Provider credential not found.");

        var now = timeProvider.GetUtcNow();
        var existing = owner is { } u2
            ? await db.Set<AiFeatureBinding>().FirstOrDefaultAsync(b => b.OwnerUserId == u2 && b.Feature == feature, ct)
            : await db.Set<AiFeatureBinding>().FirstOrDefaultAsync(b => b.OwnerUserId == null && b.Feature == feature, ct);
        if (existing is null)
            db.Set<AiFeatureBinding>().Add(AiFeatureBinding.Create(owner, feature, credentialId, now));
        else
            existing.Retarget(credentialId, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearBindingAsync(UserId? owner, AiFeature feature, CancellationToken ct)
    {
        var existing = owner is { } uid
            ? await db.Set<AiFeatureBinding>().FirstOrDefaultAsync(b => b.OwnerUserId == uid && b.Feature == feature, ct)
            : await db.Set<AiFeatureBinding>().FirstOrDefaultAsync(b => b.OwnerUserId == null && b.Feature == feature, ct);
        if (existing is null) return;
        db.Set<AiFeatureBinding>().Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    private static string CacheKey(UserId? user) =>
        user is { } u ? $"{AiConstants.ActiveProviderCacheKey}:{u.Value}" : AiConstants.ActiveProviderCacheKey;

    public Task<IReadOnlyList<AiProviderView>> ListAsync(CancellationToken ct) => ListScopedAsync(null, ct);

    public Task<IReadOnlyList<AiProviderView>> ListForUserAsync(UserId user, CancellationToken ct) =>
        ListScopedAsync(user, ct);

    private async Task<IReadOnlyList<AiProviderView>> ListScopedAsync(UserId? owner, CancellationToken ct)
    {
        var rows = await db.Set<AiProviderCredential>().AsNoTracking().Where(c => c.OwnerUserId == owner)
            .OrderByDescending(c => c.IsActive).ThenByDescending(c => c.CreatedAt).ToListAsync(ct);
        return rows.Select(c => new AiProviderView(
            c.Id.Value, c.Kind, c.BaseUrl, c.Model, c.HasKey, c.IsActive, c.MaxTokens, c.Capabilities)).ToList();
    }

    public Task<Guid> UpsertAsync(UpsertAiProviderCommand command, CancellationToken ct) =>
        UpsertScopedAsync(null, command, ct);

    public Task<Guid> UpsertForUserAsync(UserId user, UpsertAiProviderCommand command, CancellationToken ct) =>
        UpsertScopedAsync(user, command, ct);

    private async Task<Guid> UpsertScopedAsync(UserId? owner, UpsertAiProviderCommand command, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var baseUrl = new AiEndpoint(command.BaseUrl);
        AiProviderPolicy.EnsureAllowed(command.Kind, baseUrl, options.CurrentValue.Branding);
        var model = new AiModelId(command.Model);
        var maxTokens = command.MaxTokens ?? AiConstants.DefaultMaxTokens;
        var caps = command.Capabilities ?? AiProviderCapabilities.DefaultFor(command.Kind);
        var encrypted = string.IsNullOrWhiteSpace(command.ApiKey) ? null : Encrypt(command.ApiKey!);

        // The keyless singleton providers (built-in ONNX, Demo) have one fixed identity per scope and the
        // built-in one is already seeded on first startup. An "add" of one that already exists must reuse
        // the existing row (retarget/activate it), never insert a duplicate — otherwise the user ends up
        // with two identical built-in rows and, when activating, a partial-unique-index conflict.
        var targetId = command.Id;
        if (targetId is null && command.Kind is AiProviderKind.BuiltInOnnx or AiProviderKind.Demo)
        {
            var existing = await db.Set<AiProviderCredential>()
                .Where(c => c.OwnerUserId == owner && c.Kind == command.Kind)
                .Select(c => c.Id).FirstOrDefaultAsync(ct);
            if (existing != default) targetId = existing.Value;
        }

        AiProviderCredential credential;
        if (targetId is { } id)
        {
            var cid = AiProviderCredentialId.From(id);
            credential = await db.Set<AiProviderCredential>()
                .FirstOrDefaultAsync(c => c.Id == cid && c.OwnerUserId == owner, ct)
                ?? throw new InvalidOperationException("Provider credential not found.");
            credential.Retarget(baseUrl, model, maxTokens, now);
            credential.OverrideCapabilities(caps, now);
            // Only overwrite the stored key when a new one is supplied (blank = keep existing).
            if (!string.IsNullOrWhiteSpace(command.ApiKey)) credential.Rotate(encrypted, now);
        }
        else
        {
            credential = AiProviderCredential.Create(command.Kind, baseUrl, model, encrypted, caps, maxTokens, now, owner);
            db.Set<AiProviderCredential>().Add(credential);
        }

        if (command.Activate)
        {
            await DeactivateOthersAndActivateAsync(credential, owner, now, ct);
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        Invalidate(owner);
        return credential.Id.Value;
    }

    public Task ActivateAsync(Guid id, CancellationToken ct) => ActivateScopedAsync(null, id, ct);

    public Task ActivateForUserAsync(UserId user, Guid id, CancellationToken ct) => ActivateScopedAsync(user, id, ct);

    private async Task ActivateScopedAsync(UserId? owner, Guid id, CancellationToken ct)
    {
        var cid = AiProviderCredentialId.From(id);
        var credential = await db.Set<AiProviderCredential>()
            .FirstOrDefaultAsync(c => c.Id == cid && c.OwnerUserId == owner, ct)
            ?? throw new InvalidOperationException("Provider credential not found.");
        await DeactivateOthersAndActivateAsync(credential, owner, timeProvider.GetUtcNow(), ct);
        Invalidate(owner);
    }

    public Task RemoveAsync(Guid id, CancellationToken ct) => RemoveScopedAsync(null, id, ct);

    public Task RemoveForUserAsync(UserId user, Guid id, CancellationToken ct) => RemoveScopedAsync(user, id, ct);

    private async Task RemoveScopedAsync(UserId? owner, Guid id, CancellationToken ct)
    {
        var cid = AiProviderCredentialId.From(id);
        var credential = await db.Set<AiProviderCredential>()
            .FirstOrDefaultAsync(c => c.Id == cid && c.OwnerUserId == owner, ct);
        if (credential is null) return;
        // Soft delete (base is ISoftDeletable) — clear active first so the partial-unique active index
        // never counts a removed row.
        credential.Deactivate(timeProvider.GetUtcNow());
        db.Set<AiProviderCredential>().Remove(credential);
        await db.SaveChangesAsync(ct);
        Invalidate(owner);
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
        Invalidate(null);
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
        Invalidate(null);
    }

    private async Task DeactivateOthersAndActivateAsync(
        AiProviderCredential target, UserId? owner, DateTimeOffset now, CancellationToken ct)
    {
        // Exclusivity is per scope: activating a user's provider only deactivates that user's others;
        // activating the deployment default only touches deployment-scoped rows.
        var others = await db.Set<AiProviderCredential>()
            .Where(c => c.OwnerUserId == owner && c.IsActive && c.Id != target.Id).ToListAsync(ct);
        if (others.Count > 0)
        {
            foreach (var other in others) other.Deactivate(now);
            // Flush the deactivations first (a newly-added target is inserted here still INACTIVE): Postgres
            // evaluates the partial-unique active index per statement and EF does not guarantee the UPDATE
            // clearing the old active row runs before the INSERT/UPDATE that sets the new one, so doing both
            // in a single batch can momentarily present two active rows in the scope -> 23505 conflict.
            await db.SaveChangesAsync(ct);
        }
        target.Activate(now);
        await db.SaveChangesAsync(ct);
    }

    private void Invalidate(UserId? owner)
    {
        cache.Remove(CacheKey(owner));
        // A deployment-default change also affects users with no own credential (they fall back to it);
        // their per-user cache entries expire on the short TTL rather than being enumerated here.
        if (owner is not null) cache.Remove(CacheKey(null));
    }

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
