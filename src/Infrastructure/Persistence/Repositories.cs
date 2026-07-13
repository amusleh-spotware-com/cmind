using Core;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class AppUserRepository(DataContext db) : IAppUserRepository
{
    public Task<AppUser?> GetByIdAsync(UserId id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public async Task AddAsync(AppUser user, CancellationToken ct) => await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class CTraderIdAccountRepository(DataContext db) : ICTraderIdAccountRepository
{
    public Task<CTraderIdAccount?> GetByIdAsync(CtidId id, UserId owner, CancellationToken ct) =>
        db.CTids.FirstOrDefaultAsync(c => c.Id == id && c.UserId == owner, ct);

    public async Task AddAsync(CTraderIdAccount account, CancellationToken ct) => await db.CTids.AddAsync(account, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class OpenApiApplicationRepository(DataContext db) : IOpenApiApplicationRepository
{
    public Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, UserId owner, CancellationToken ct) =>
        db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == id && a.UserId == owner, ct);

    public Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, CancellationToken ct) =>
        db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<OpenApiApplication?> GetByUserAsync(UserId owner, CancellationToken ct) =>
        db.OpenApiApplications.FirstOrDefaultAsync(a => a.UserId == owner && !a.IsShared, ct);

    public Task<OpenApiApplication?> GetSharedAsync(CancellationToken ct) =>
        db.OpenApiApplications.FirstOrDefaultAsync(a => a.IsShared, ct);

    public async Task AddAsync(OpenApiApplication application, CancellationToken ct) =>
        await db.OpenApiApplications.AddAsync(application, ct);

    public Task RemoveAsync(OpenApiApplication application, CancellationToken ct)
    {
        db.OpenApiApplications.Remove(application);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class OpenApiAuthorizationRepository(DataContext db) : IOpenApiAuthorizationRepository
{
    public Task<OpenApiAuthorization?> GetByIdAsync(OpenApiAuthorizationId id, UserId owner, CancellationToken ct) =>
        db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == id && a.UserId == owner, ct);

    public Task<OpenApiAuthorization?> GetByCtidUserIdAsync(long ctidUserId, UserId owner, CancellationToken ct) =>
        db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.CtidUserId == ctidUserId && a.UserId == owner, ct);

    public async Task<IReadOnlyList<OpenApiAuthorization>> GetExpiringAsync(DateTimeOffset threshold, CancellationToken ct) =>
        await db.OpenApiAuthorizations.Where(a => a.AccessTokenExpiresAt <= threshold).ToListAsync(ct);

    public async Task AddAsync(OpenApiAuthorization authorization, CancellationToken ct) =>
        await db.OpenApiAuthorizations.AddAsync(authorization, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class CopyProfileRepository(DataContext db) : ICopyProfileRepository
{
    public Task<CopyProfile?> GetByIdAsync(CopyProfileId id, UserId owner, CancellationToken ct) =>
        db.CopyProfiles.FirstOrDefaultAsync(p => p.Id == id && p.UserId == owner, ct);

    public Task<CopyProfile?> GetWithDestinationsAsync(CopyProfileId id, CancellationToken ct) =>
        db.CopyProfiles.Include(p => p.Destinations).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(CopyProfile profile, CancellationToken ct) => await db.CopyProfiles.AddAsync(profile, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class McpApiKeyRepository(DataContext db) : IMcpApiKeyRepository
{
    public Task<McpApiKey?> GetByIdAsync(McpApiKeyId id, UserId owner, CancellationToken ct) =>
        db.McpApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == owner, ct);

    public async Task AddAsync(McpApiKey key, CancellationToken ct) => await db.McpApiKeys.AddAsync(key, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class CBotRepository(DataContext db) : ICBotRepository
{
    public Task<CBot?> GetByIdAsync(CBotId id, UserId owner, CancellationToken ct) =>
        db.CBots.FirstOrDefaultAsync(c => c.Id == id && c.UserId == owner, ct);

    public async Task AddAsync(CBot cbot, CancellationToken ct) => await db.CBots.AddAsync(cbot, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class AgentMandateRepository(DataContext db) : IAgentMandateRepository
{
    public Task<AgentMandate?> GetByIdAsync(AgentMandateId id, UserId owner, CancellationToken ct) =>
        db.AgentMandates.FirstOrDefaultAsync(m => m.Id == id && m.UserId == owner, ct);

    public Task<AgentMandate?> GetWithProposalsAsync(AgentMandateId id, CancellationToken ct) =>
        db.AgentMandates.Include(m => m.Proposals).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(AgentMandate mandate, CancellationToken ct) => await db.AgentMandates.AddAsync(mandate, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class AlertRuleRepository(DataContext db) : IAlertRuleRepository
{
    public Task<AlertRule?> GetByIdAsync(AlertRuleId id, UserId owner, CancellationToken ct) =>
        db.AlertRules.FirstOrDefaultAsync(r => r.Id == id && r.UserId == owner, ct);

    public async Task AddAsync(AlertRule rule, CancellationToken ct) => await db.AlertRules.AddAsync(rule, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class JournalNoteRepository(DataContext db) : IJournalNoteRepository
{
    public Task<Core.Journal.JournalNote?> GetByIdAsync(JournalNoteId id, UserId owner, CancellationToken ct) =>
        db.JournalNotes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == owner, ct);

    public async Task<IReadOnlyList<Core.Journal.JournalNote>> ListByUserAsync(UserId owner, CancellationToken ct) =>
        await db.JournalNotes.Where(n => n.UserId == owner)
            .OrderByDescending(n => n.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Core.Journal.JournalNote note, CancellationToken ct) =>
        await db.JournalNotes.AddAsync(note, ct);

    public void Remove(Core.Journal.JournalNote note) => db.JournalNotes.Remove(note);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class PropRuleRepository(DataContext db) : IPropRuleRepository
{
    public Task<PropRule?> GetByIdAsync(PropRuleId id, UserId owner, CancellationToken ct) =>
        db.PropRules.FirstOrDefaultAsync(r => r.Id == id && r.UserId == owner, ct);

    public async Task AddAsync(PropRule rule, CancellationToken ct) => await db.PropRules.AddAsync(rule, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class PropFirmChallengeRepository(DataContext db) : IPropFirmChallengeRepository
{
    public Task<PropFirmChallenge?> GetByIdAsync(PropFirmChallengeId id, UserId owner, CancellationToken ct) =>
        db.PropFirmChallenges.FirstOrDefaultAsync(c => c.Id == id && c.UserId == owner, ct);

    public async Task<IReadOnlyList<PropFirmChallenge>> ListByUserAsync(UserId owner, CancellationToken ct) =>
        await db.PropFirmChallenges.Where(c => c.UserId == owner)
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(PropFirmChallenge challenge, CancellationToken ct) =>
        await db.PropFirmChallenges.AddAsync(challenge, ct);

    public void Remove(PropFirmChallenge challenge) => db.PropFirmChallenges.Remove(challenge);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class LegalDocumentRepository(DataContext db) : ILegalDocumentRepository
{
    public Task<LegalDocument?> GetActiveAsync(LegalDocumentType type, CancellationToken ct) =>
        db.LegalDocuments.Where(d => d.Type == type && d.Published)
            .OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LegalDocument>> ListActiveAsync(CancellationToken ct)
    {
        var published = await db.LegalDocuments.Where(d => d.Published).ToListAsync(ct);
        return published.GroupBy(d => d.Type)
            .Select(g => g.OrderByDescending(d => d.Version).First())
            .ToList();
    }

    public Task<LegalDocument?> GetByIdAsync(LegalDocumentId id, CancellationToken ct) =>
        db.LegalDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddAsync(LegalDocument document, CancellationToken ct) =>
        await db.LegalDocuments.AddAsync(document, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class ConsentRepository(DataContext db) : IConsentRepository
{
    public async Task<IReadOnlyList<ConsentRecord>> ListByUserAsync(UserId userId, CancellationToken ct) =>
        await db.ConsentRecords.Where(c => c.UserId == userId).ToListAsync(ct);

    public Task<bool> HasConsentAsync(UserId userId, LegalDocumentType type, int version, CancellationToken ct) =>
        db.ConsentRecords.AnyAsync(c => c.UserId == userId && c.DocumentType == type && c.Version == version, ct);

    public async Task AddAsync(ConsentRecord record, CancellationToken ct) =>
        await db.ConsentRecords.AddAsync(record, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
