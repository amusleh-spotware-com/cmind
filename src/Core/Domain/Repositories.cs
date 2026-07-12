namespace Core.Domain;

public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(UserId id, CancellationToken ct);
    Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct);
    Task AddAsync(AppUser user, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ICTraderIdAccountRepository
{
    Task<CTraderIdAccount?> GetByIdAsync(CtidId id, UserId owner, CancellationToken ct);
    Task AddAsync(CTraderIdAccount account, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IOpenApiApplicationRepository
{
    Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, UserId owner, CancellationToken ct);

    // Owner-agnostic lookup for trusted server-side resolution (token refresh, callback): the caller has
    // already scoped ownership via the authorization, and a shared app is owned by the operator, not the
    // authorizing user.
    Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, CancellationToken ct);
    Task<OpenApiApplication?> GetByUserAsync(UserId owner, CancellationToken ct);

    // The single deployment-wide shared application, if configured (drives shared-mode).
    Task<OpenApiApplication?> GetSharedAsync(CancellationToken ct);
    Task AddAsync(OpenApiApplication application, CancellationToken ct);
    Task RemoveAsync(OpenApiApplication application, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IOpenApiAuthorizationRepository
{
    Task<OpenApiAuthorization?> GetByIdAsync(OpenApiAuthorizationId id, UserId owner, CancellationToken ct);
    Task<OpenApiAuthorization?> GetByCtidUserIdAsync(long ctidUserId, UserId owner, CancellationToken ct);
    Task<IReadOnlyList<OpenApiAuthorization>> GetExpiringAsync(DateTimeOffset threshold, CancellationToken ct);
    Task AddAsync(OpenApiAuthorization authorization, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ICopyProfileRepository
{
    Task<CopyProfile?> GetByIdAsync(CopyProfileId id, UserId owner, CancellationToken ct);
    Task<CopyProfile?> GetWithDestinationsAsync(CopyProfileId id, CancellationToken ct);
    Task AddAsync(CopyProfile profile, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IMcpApiKeyRepository
{
    Task<McpApiKey?> GetByIdAsync(McpApiKeyId id, UserId owner, CancellationToken ct);
    Task AddAsync(McpApiKey key, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ICBotRepository
{
    Task<CBot?> GetByIdAsync(CBotId id, UserId owner, CancellationToken ct);
    Task AddAsync(CBot cbot, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAgentMandateRepository
{
    Task<AgentMandate?> GetByIdAsync(AgentMandateId id, UserId owner, CancellationToken ct);
    Task<AgentMandate?> GetWithProposalsAsync(AgentMandateId id, CancellationToken ct);
    Task AddAsync(AgentMandate mandate, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAlertRuleRepository
{
    Task<AlertRule?> GetByIdAsync(AlertRuleId id, UserId owner, CancellationToken ct);
    Task AddAsync(AlertRule rule, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPropRuleRepository
{
    Task<PropRule?> GetByIdAsync(PropRuleId id, UserId owner, CancellationToken ct);
    Task AddAsync(PropRule rule, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPropFirmChallengeRepository
{
    Task<PropFirmChallenge?> GetByIdAsync(PropFirmChallengeId id, UserId owner, CancellationToken ct);
    Task<IReadOnlyList<PropFirmChallenge>> ListByUserAsync(UserId owner, CancellationToken ct);
    Task AddAsync(PropFirmChallenge challenge, CancellationToken ct);
    void Remove(PropFirmChallenge challenge);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ILegalDocumentRepository
{
    Task<LegalDocument?> GetActiveAsync(LegalDocumentType type, CancellationToken ct);
    Task<IReadOnlyList<LegalDocument>> ListActiveAsync(CancellationToken ct);
    Task<LegalDocument?> GetByIdAsync(LegalDocumentId id, CancellationToken ct);
    Task AddAsync(LegalDocument document, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IConsentRepository
{
    Task<IReadOnlyList<ConsentRecord>> ListByUserAsync(UserId userId, CancellationToken ct);
    Task<bool> HasConsentAsync(UserId userId, LegalDocumentType type, int version, CancellationToken ct);
    Task AddAsync(ConsentRecord record, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
