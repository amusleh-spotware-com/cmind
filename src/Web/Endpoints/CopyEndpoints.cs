using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateCopyProfileRequest(string Name, Guid SourceAccountId);

public record AddCopyDestinationRequest(
    Guid DestinationAccountId,
    MoneyManagementMode Mode,
    double Parameter,
    double SlippagePips,
    int MaxDelaySeconds,
    bool Reverse,
    bool CopyStopLoss,
    bool CopyTakeProfit,
    CopyDirectionFilter Direction,
    double MinLot,
    double MaxLot,
    bool ForceMinLot,
    double MaxDrawdownPercent,
    double DailyLossLimit,
    SymbolFilterMode SymbolFilterMode = SymbolFilterMode.None,
    IReadOnlyList<string>? SymbolFilters = null,
    IReadOnlyList<SymbolMapPair>? SymbolMap = null);

public record SymbolMapPair(string Source, string Destination);

public static class CopyEndpoints
{
    public static IEndpointRouteBuilder MapCopyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/copy").RequireAuthorization(AuthPolicies.UserOrAbove);

        g.MapGet("/profiles", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var profiles = await db.CopyProfiles.Include(p => p.Destinations)
                .Where(p => p.UserId == uid).ToListAsync();
            return profiles.Select(p => new
            {
                p.Id,
                p.Name,
                SourceAccountId = p.SourceAccountId.Value,
                Status = p.Status.ToString(),
                DestinationCount = p.Destinations.Count
            });
        });

        g.MapGet("/accounts/{tradingAccountId:guid}/symbols", async (Guid tradingAccountId, DataContext db,
            ICurrentUser u, ISecretProtector p, IOpenApiClient client, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var tid = TradingAccountId.From(tradingAccountId);
            var account = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.Id == tid && t.CTid.UserId == uid, ct);
            if (account?.OpenApiAuthorizationId is null || account.CtidTraderAccountId is null)
                return Results.BadRequest("Account is not Open API linked.");

            var auth = await db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == account.OpenApiAuthorizationId, ct);
            var application = auth is null ? null
                : await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == auth.ApplicationId, ct);
            if (auth is null || application is null) return Results.NotFound();

            var secret = Encoding.UTF8.GetString(p.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
            var token = Encoding.UTF8.GetString(p.Unprotect(auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken));
            try
            {
                return Results.Ok(await client.GetSymbolNamesAsync(
                    account.IsLive, application.ClientId, secret, token, account.CtidTraderAccountId.Value, ct));
            }
            catch (Exception)
            {
                return Results.Ok(Array.Empty<string>());
            }
        });

        g.MapGet("/profiles/{id:guid}", async (Guid id, ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            return Results.Ok(new
            {
                profile.Id,
                profile.Name,
                SourceAccountId = profile.SourceAccountId.Value,
                Status = profile.Status.ToString(),
                Destinations = profile.Destinations.Select(d => new
                {
                    d.Id,
                    DestinationAccountId = d.DestinationAccountId.Value,
                    Mode = d.RiskMode.ToString(),
                    d.RiskParameter,
                    d.SlippagePips,
                    d.MaxDelaySeconds,
                    d.Reverse,
                    d.CopyStopLoss,
                    d.CopyTakeProfit,
                    Direction = d.Direction.ToString(),
                    d.MinLot,
                    d.MaxLot,
                    d.ForceMinLot,
                    d.MaxDrawdownPercent,
                    d.DailyLossLimit,
                    SymbolFilterMode = d.SymbolFilterMode.ToString(),
                    SymbolFilters = d.SymbolFilters.Select(f => f.Symbol),
                    SymbolMaps = d.SymbolMaps.Select(m => new { m.Source, m.Destination })
                })
            });
        });

        g.MapPost("/profiles", async (CreateCopyProfileRequest req, ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var profile = CopyProfile.Create(uid, req.Name, TradingAccountId.From(req.SourceAccountId));
            await repo.AddAsync(profile, ct);
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { profile.Id });
        });

        g.MapDelete("/profiles/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var pid = CopyProfileId.From(id);
            var profile = await db.CopyProfiles.FirstOrDefaultAsync(p => p.Id == pid && p.UserId == u.UserId!.Value, ct);
            if (profile is null) return Results.NotFound();
            db.CopyProfiles.Remove(profile);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/profiles/{id:guid}/destinations", async (Guid id, AddCopyDestinationRequest req,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();

            var destination = profile.AddDestination(
                TradingAccountId.From(req.DestinationAccountId), new RiskSettings(req.Mode, req.Parameter));
            destination.ConfigureSlippage(new SlippagePips(req.SlippagePips));
            destination.ConfigureMaxDelay(MaxCopyDelay.Seconds(req.MaxDelaySeconds));
            destination.ConfigureBounds(new LotBounds(req.MinLot, req.MaxLot, req.ForceMinLot));
            destination.SetReverse(req.Reverse);
            destination.SetCopyProtection(req.CopyStopLoss, req.CopyTakeProfit);
            destination.SetDirection(req.Direction);
            destination.SetGuards(new DrawdownPercent(req.MaxDrawdownPercent), req.DailyLossLimit);
            if (req.SymbolMap is { Count: > 0 })
                destination.SetSymbolMap(req.SymbolMap.Select(m => new SymbolMapEntry(new Symbol(m.Source), new Symbol(m.Destination))));
            if (req.SymbolFilterMode != SymbolFilterMode.None && req.SymbolFilters is { Count: > 0 })
                destination.SetSymbolFilter(req.SymbolFilterMode, req.SymbolFilters.Select(s => new Symbol(s)));
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { destination.Id });
        });

        g.MapDelete("/profiles/{id:guid}/destinations/{destinationId:guid}", async (Guid id, Guid destinationId,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            profile.RemoveDestination(CopyDestinationId.From(destinationId));
            await repo.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/profiles/{id:guid}/{action}", async (Guid id, string action,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetByIdAsync(CopyProfileId.From(id), u.UserId!.Value, ct);
            if (profile is null) return Results.NotFound();
            switch (action)
            {
                case "start": profile.Start(); break;
                case "pause": profile.Pause(); break;
                case "stop": profile.Stop(); break;
                default: return Results.NotFound();
            }
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { Status = profile.Status.ToString() });
        });

        return app;
    }
}
