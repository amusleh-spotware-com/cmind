using Core.Ai;
using Core.Constants;
using Core.Domain;

namespace Core;

/// <summary>
/// A configured AI provider the app can talk to: its wire kind, endpoint, model, an optional encrypted
/// API key (local endpoints need none), capability flags and token budget. Replaces the single-key
/// model. Exactly one credential is <see cref="IsActive"/> at a time; the invariant is enforced through
/// <see cref="Activate"/>/<see cref="Deactivate"/> plus the store (one aggregate per transaction).
/// </summary>
public class AiProviderCredential : AuditedEntity<AiProviderCredentialId>
{
    public AiProviderKind Kind { get; private set; }
    public string BaseUrl { get; private set; } = default!;
    public string Model { get; private set; } = default!;

    // Null for a keyless local endpoint; otherwise the API key encrypted via ISecretProtector.
    public byte[]? EncryptedApiKey { get; private set; }

    public int MaxTokens { get; private set; }
    public bool IsActive { get; private set; }

    public bool SupportsWebSearch { get; private set; }
    public bool SupportsVision { get; private set; }
    public bool SupportsSystemRole { get; private set; }
    public bool SupportsTools { get; private set; }

    public AiProviderCapabilities Capabilities =>
        new(SupportsWebSearch, SupportsVision, SupportsSystemRole, SupportsTools);

    public bool HasKey => EncryptedApiKey is { Length: > 0 };

    public static AiProviderCredential Create(
        AiProviderKind kind,
        AiEndpoint baseUrl,
        AiModelId model,
        byte[]? encryptedApiKey,
        AiProviderCapabilities capabilities,
        int maxTokens,
        DateTimeOffset now)
    {
        var credential = new AiProviderCredential
        {
            Kind = kind,
            BaseUrl = baseUrl.Value,
            Model = model.Value,
            EncryptedApiKey = NormalizeKey(encryptedApiKey),
            MaxTokens = GuardTokens(maxTokens)
        };
        credential.ApplyCapabilities(capabilities);
        credential.PreserveCreatedAt(now);
        return credential;
    }

    public void Rotate(byte[]? encryptedApiKey, DateTimeOffset now)
    {
        EncryptedApiKey = NormalizeKey(encryptedApiKey);
        Touch(now);
    }

    public void Retarget(AiEndpoint baseUrl, AiModelId model, int maxTokens, DateTimeOffset now)
    {
        BaseUrl = baseUrl.Value;
        Model = model.Value;
        MaxTokens = GuardTokens(maxTokens);
        Touch(now);
    }

    public void OverrideCapabilities(AiProviderCapabilities capabilities, DateTimeOffset now)
    {
        ApplyCapabilities(capabilities);
        Touch(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        Touch(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        Touch(now);
    }

    private void ApplyCapabilities(AiProviderCapabilities capabilities)
    {
        SupportsWebSearch = capabilities.SupportsWebSearch;
        SupportsVision = capabilities.SupportsVision;
        SupportsSystemRole = capabilities.SupportsSystemRole;
        SupportsTools = capabilities.SupportsTools;
    }

    private void Touch(DateTimeOffset now) => PreserveUpdatedAt(now);

    private static byte[]? NormalizeKey(byte[]? key) => key is { Length: > 0 } ? key : null;

    private static int GuardTokens(int maxTokens)
    {
        DomainGuard.AgainstOutOfInclusiveRange(maxTokens, 1, 200_000, DomainErrors.AiMaxTokensOutOfRange);
        return maxTokens;
    }
}
