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

    // The deployment-wide shared application a white-label operator ships so all users authorize their
    // accounts through one Open API app. Owned by the owner account; at most one may exist (filtered
    // unique index). When present the app runs in shared-mode: users cannot register their own app.
    public bool IsShared { get; private set; }

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

    public static OpenApiApplication CreateShared(
        UserId ownerId,
        string name,
        OpenApiClientId clientId,
        byte[] encryptedClientSecret,
        OpenApiRedirectUri redirectUri)
        => new()
        {
            UserId = ownerId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            ClientId = clientId.Value,
            EncryptedClientSecret = GuardSecret(encryptedClientSecret),
            RedirectUri = redirectUri.Value,
            IsShared = true
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
    public int ConsecutiveRefreshFailures { get; private set; }
    public bool RefreshCriticalAlerted { get; private set; }
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

    public void Refresh(byte[] encryptedAccessToken, byte[] encryptedRefreshToken, DateTimeOffset accessTokenExpiresAt,
        DateTimeOffset now)
    {
        EncryptedAccessToken = GuardToken(encryptedAccessToken);
        EncryptedRefreshToken = GuardToken(encryptedRefreshToken);
        AccessTokenExpiresAt = accessTokenExpiresAt;
        LastRefreshedAt = now;
        RefreshFailedAt = null;
        ConsecutiveRefreshFailures = 0;
        RefreshCriticalAlerted = false;
        TokenVersion++;
        RaiseDomainEvent(new AccessTokenRefreshed(Id, UserId, CtidUserId));
    }

    // Records a failed refresh attempt. Always raises the per-failure warning event; additionally
    // escalates once with a critical event when the token is now within <paramref name="criticalWindow"/>
    // of expiry and still failing, so the owner is alerted before the token dies. Returns whether this
    // call escalated. The escalation latch and failure counter reset on a successful <see cref="Refresh"/>.
    public bool MarkRefreshFailed(string reason, DateTimeOffset now, TimeSpan criticalWindow)
    {
        RefreshFailedAt = now;
        ConsecutiveRefreshFailures++;
        RaiseDomainEvent(new AccessTokenRefreshFailed(Id, UserId, reason ?? string.Empty));

        if (RefreshCriticalAlerted || now < AccessTokenExpiresAt - criticalWindow)
            return false;

        RefreshCriticalAlerted = true;
        RaiseDomainEvent(new AccessTokenRefreshCritical(
            Id, UserId, CtidUserId, AccessTokenExpiresAt, ConsecutiveRefreshFailures));
        return true;
    }

    public bool IsExpiring(TimeSpan threshold, DateTimeOffset now) => now >= AccessTokenExpiresAt - threshold;

    // Re-point this authorization at the deployment shared application when a white-label operator switches
    // to shared-mode. The stored tokens were issued under the old app's client id, so a re-authorization is
    // required to obtain valid tokens for the shared app — the next refresh fails and escalates until then.
    public void ReassignToApplication(OpenApiApplicationId applicationId) => ApplicationId = applicationId;

    private static byte[] GuardToken(byte[] token)
    {
        if (token is null || token.Length == 0) throw new DomainException(DomainErrors.OpenApiTokenRequired);
        return token;
    }
}
