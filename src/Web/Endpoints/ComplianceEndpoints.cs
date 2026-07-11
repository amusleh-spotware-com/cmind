using Core;
using Core.Constants;
using Core.Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateLegalDocumentRequest(LegalDocumentType Type, int Version, string Body);

public record AcceptConsentRequest(string Type);

public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/compliance").RequireAuthorization(AuthPolicies.UserOrAbove)
            .RequireFeature(Core.Features.FeatureFlag.Compliance);

        g.MapGet("/documents/active", async (ILegalDocumentRepository repo, CancellationToken ct) =>
            Results.Ok((await repo.ListActiveAsync(ct))
                .Select(d => new { Type = d.Type.ToString(), d.Version, d.Body })));

        g.MapGet("/consent/status", async (
            ILegalDocumentRepository docs, IConsentRepository consents, ICurrentUser u, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var result = new List<object>();
            foreach (var d in await docs.ListActiveAsync(ct))
                result.Add(new
                {
                    Type = d.Type.ToString(),
                    d.Version,
                    Consented = await consents.HasConsentAsync(uid, d.Type, d.Version, ct)
                });
            return Results.Ok(result);
        });

        g.MapPost("/consent", async (
            AcceptConsentRequest req, ILegalDocumentRepository docs, IConsentRepository consents,
            ICurrentUser u, HttpContext http, TimeProvider clock, CancellationToken ct) =>
        {
            if (!Enum.TryParse<LegalDocumentType>(req.Type, ignoreCase: true, out var type))
                return Results.BadRequest(new { error = "unknown document type" });
            var uid = u.UserId!.Value;
            var active = await docs.GetActiveAsync(type, ct);
            if (active is null) return Results.BadRequest(new { error = "no published document for type" });
            if (await consents.HasConsentAsync(uid, type, active.Version, ct))
                return Results.Ok(new { alreadyConsented = true });

            var ip = http.Connection.RemoteIpAddress?.ToString();
            await consents.AddAsync(ConsentRecord.Accept(uid, type, active.Version, clock.GetUtcNow(), ip), ct);
            await consents.SaveChangesAsync(ct);
            return Results.Ok(new { Type = type.ToString(), active.Version });
        });

        g.MapGet("/export", async (DataContext db, IConsentRepository consents, ICurrentUser u, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (user is null) return Results.NotFound();
            return Results.Ok(new
            {
                user = new { id = uid.Value, user.Email, user.RoleName, user.CreatedAt },
                consents = (await consents.ListByUserAsync(uid, ct))
                    .Select(c => new { Type = c.DocumentType.ToString(), c.Version, c.AcceptedAt }),
                copyProfiles = await db.CopyProfiles.AsNoTracking().Where(p => p.UserId == uid)
                    .Select(p => p.Name).ToListAsync(ct),
                propFirmChallenges = await db.PropFirmChallenges.AsNoTracking().Where(c => c.UserId == uid)
                    .Select(c => c.Name).ToListAsync(ct)
            });
        });

        g.MapPost("/erase", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (user is null) return Results.NotFound();
            user.Anonymize();
            db.Users.Remove(user);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { erased = true });
        });

        var owner = app.MapGroup("/api/compliance").RequireAuthorization(AuthPolicies.Owner)
            .RequireFeature(Core.Features.FeatureFlag.Compliance);

        owner.MapPost("/documents", async (CreateLegalDocumentRequest req, ILegalDocumentRepository repo, CancellationToken ct) =>
        {
            try
            {
                var doc = LegalDocument.Draft(req.Type, req.Version, req.Body);
                await repo.AddAsync(doc, ct);
                await repo.SaveChangesAsync(ct);
                return Results.Ok(new { doc.Id });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        owner.MapPost("/documents/{id:guid}/publish", async (
            Guid id, ILegalDocumentRepository repo, TimeProvider clock, CancellationToken ct) =>
        {
            var doc = await repo.GetByIdAsync(LegalDocumentId.From(id), ct);
            if (doc is null) return Results.NotFound();
            try
            {
                doc.Publish(clock.GetUtcNow());
                await repo.SaveChangesAsync(ct);
                return Results.Ok(new { doc.Id, doc.Published });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        owner.MapGet("/audit/verify", async (IAuditTrailVerifier verifier, CancellationToken ct) =>
            Results.Ok(await verifier.VerifyAsync(ct)));

        return app;
    }
}
