using Core.Ai;

namespace Core.Domain;

/// <summary>
/// Binds one <see cref="AiFeature"/> to the provider credential that should serve it, so different AI
/// features can run on different models at the same time. <see cref="OwnerUserId"/> <c>null</c> = deployment
/// scope (the owner-managed default binding for every user); a value = a specific user's own binding, which
/// overrides the deployment binding for that user. A feature with no binding falls back to the scope's active
/// provider. At most one binding exists per (scope, feature) — enforced by a unique index and
/// <see cref="Retarget"/> reusing the existing row.
/// </summary>
public sealed class AiFeatureBinding : AuditedEntity<AiFeatureBindingId>
{
    private AiFeatureBinding() { }

    public UserId? OwnerUserId { get; private set; }

    public bool IsDeploymentScoped => OwnerUserId is null;

    public AiFeature Feature { get; private set; }

    public AiProviderCredentialId CredentialId { get; private set; }

    public static AiFeatureBinding Create(
        UserId? ownerUserId, AiFeature feature, AiProviderCredentialId credentialId, DateTimeOffset now)
    {
        var binding = new AiFeatureBinding
        {
            OwnerUserId = ownerUserId,
            Feature = feature,
            CredentialId = credentialId
        };
        binding.PreserveCreatedAt(now);
        return binding;
    }

    /// <summary>Point this feature at a different provider credential.</summary>
    public void Retarget(AiProviderCredentialId credentialId, DateTimeOffset now)
    {
        CredentialId = credentialId;
        PreserveUpdatedAt(now);
    }
}
