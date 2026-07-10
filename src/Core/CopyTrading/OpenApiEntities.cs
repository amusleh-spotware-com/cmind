using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core;

// ---------------- Copy trading: Open API OAuth application + per-cTID authorization ----------------

public class OpenApiApplication : AuditedEntity<OpenApiApplicationId>
{
    public UserId UserId { get; private set; }
    [MaxLength(128)] public string Name { get; private set; } = default!;
    [MaxLength(128)] public string ClientId { get; private set; } = default!;
    public byte[] EncryptedClientSecret { get; private set; } = default!;
    [MaxLength(512)] public string RedirectUri { get; private set; } = default!;

    public static OpenApiApplication Create(
        UserId userId,
        string name,
        OpenApiClientId clientId,
        byte[] encryptedClientSecret,
        OpenApiRedirectUri redirectUri)
        => new()
        {
            UserId = userId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            ClientId = clientId.Value,
            EncryptedClientSecret = GuardSecret(encryptedClientSecret),
            RedirectUri = redirectUri.Value
        };

    public void UpdateCredentials(
        string name,
        OpenApiClientId clientId,
        byte[] encryptedClientSecret,
        OpenApiRedirectUri redirectUri)
    {
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
        ClientId = clientId.Value;
        EncryptedClientSecret = GuardSecret(encryptedClientSecret);
        RedirectUri = redirectUri.Value;
        Touch();
    }

    private static byte[] GuardSecret(byte[] secret)
    {
        if (secret is null || secret.Length == 0) throw new DomainException(DomainErrors.OpenApiSecretRequired);
        return secret;
    }
}

public class OpenApiAuthorization : AuditedEntity<OpenApiAuthorizationId>
{
    public UserId UserId { get; private set; }
    public OpenApiApplicationId ApplicationId { get; private set; }
    public long CtidUserId { get; private set; }
    public bool IsLive { get; private set; }
    public byte[] EncryptedAccessToken { get; private set; } = default!;
    public byte[] EncryptedRefreshToken { get; private set; } = default!;
    public DateTimeOffset AccessTokenExpiresAt { get; private set; }
    public OpenApiScope Scope { get; private set; }
    public DateTimeOffset? LastRefreshedAt { get; private set; }
    public DateTimeOffset? RefreshFailedAt { get; private set; }
    public long TokenVersion { get; private set; } = 1;

    public static OpenApiAuthorization Create(
        UserId userId,
        OpenApiApplicationId applicationId,
        CtidUserId ctidUserId,
        bool isLive,
        byte[] encryptedAccessToken,
        byte[] encryptedRefreshToken,
        DateTimeOffset accessTokenExpiresAt,
        OpenApiScope scope)
    {
        var authorization = new OpenApiAuthorization
        {
            UserId = userId,
            ApplicationId = applicationId,
            CtidUserId = ctidUserId.Value,
            IsLive = isLive,
            EncryptedAccessToken = GuardToken(encryptedAccessToken),
            EncryptedRefreshToken = GuardToken(encryptedRefreshToken),
            AccessTokenExpiresAt = accessTokenExpiresAt,
            Scope = scope
        };
        authorization.RaiseDomainEvent(
            new OpenApiAccountAuthorized(authorization.Id, userId, ctidUserId.Value));
        return authorization;
    }

    public void Refresh(byte[] encryptedAccessToken, byte[] encryptedRefreshToken, DateTimeOffset accessTokenExpiresAt)
    {
        EncryptedAccessToken = GuardToken(encryptedAccessToken);
        EncryptedRefreshToken = GuardToken(encryptedRefreshToken);
        AccessTokenExpiresAt = accessTokenExpiresAt;
        LastRefreshedAt = DateTimeOffset.UtcNow;
        RefreshFailedAt = null;
        TokenVersion++;
        Touch();
        RaiseDomainEvent(new AccessTokenRefreshed(Id, UserId, CtidUserId));
    }

    public void MarkRefreshFailed(string reason)
    {
        RefreshFailedAt = DateTimeOffset.UtcNow;
        Touch();
        RaiseDomainEvent(new AccessTokenRefreshFailed(Id, UserId, reason ?? string.Empty));
    }

    public bool IsExpiring(TimeSpan threshold, DateTimeOffset now) => now >= AccessTokenExpiresAt - threshold;

    private static byte[] GuardToken(byte[] token)
    {
        if (token is null || token.Length == 0) throw new DomainException(DomainErrors.OpenApiTokenRequired);
        return token;
    }
}
