using Core;
using Core.Constants;
using Core.Domain;
using Core.PropFirm;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Endpoints;

public record CreatePropFirmChallengeRequest(
    string Name,
    Guid TradingAccountId,
    decimal StartingBalance,
    double ProfitTargetPercent,
    double MaxDailyLossPercent,
    double MaxTotalDrawdownPercent,
    DrawdownMode DrawdownMode,
    int MinTradingDays,
    bool SingleStep);

public record RecordEquityRequest(decimal Equity);

public static class PropFirmEndpoints
{
    public static IEndpointRouteBuilder MapPropFirmEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/prop-firm").RequireAuthorization(AuthPolicies.UserOrAbove)
            .RequireFeature(Core.Features.FeatureFlag.PropFirm);

        g.MapGet("/challenges", async (IPropFirmChallengeRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var challenges = await repo.ListByUserAsync(u.UserId!.Value, ct);
            return Results.Ok(challenges.Select(Project));
        });

        g.MapGet("/challenges/{id:guid}", async (Guid id, IPropFirmChallengeRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var challenge = await repo.GetByIdAsync(PropFirmChallengeId.From(id), u.UserId!.Value, ct);
            return challenge is null ? Results.NotFound() : Results.Ok(Project(challenge));
        });

        g.MapPost("/challenges", async (CreatePropFirmChallengeRequest req, IPropFirmChallengeRepository repo,
            ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            try
            {
                var rules = new ChallengeRules(
                    new Percent(req.ProfitTargetPercent),
                    new Percent(req.MaxDailyLossPercent),
                    new Percent(req.MaxTotalDrawdownPercent),
                    req.DrawdownMode,
                    new TradingDayRequirement(req.MinTradingDays),
                    req.SingleStep);
                var challenge = PropFirmChallenge.Create(uid, TradingAccountId.From(req.TradingAccountId),
                    req.Name, new Money(req.StartingBalance), rules);
                await repo.AddAsync(challenge, ct);
                await repo.SaveChangesAsync(ct);
                return Results.Ok(new { challenge.Id });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/challenges/{id:guid}/equity", async (Guid id, RecordEquityRequest req,
            IPropFirmChallengeRepository repo, ICurrentUser u, TimeProvider clock, CancellationToken ct) =>
        {
            var challenge = await repo.GetByIdAsync(PropFirmChallengeId.From(id), u.UserId!.Value, ct);
            if (challenge is null) return Results.NotFound();
            try
            {
                challenge.RecordEquity(new Money(req.Equity), clock.GetUtcNow());
                await repo.SaveChangesAsync(ct);
                return Results.Ok(Project(challenge));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapDelete("/challenges/{id:guid}", async (Guid id, IPropFirmChallengeRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var challenge = await repo.GetByIdAsync(PropFirmChallengeId.From(id), u.UserId!.Value, ct);
            if (challenge is null) return Results.NotFound();
            repo.Remove(challenge);
            await repo.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }

    private static object Project(PropFirmChallenge c) => new
    {
        Id = c.Id.Value,
        c.Name,
        TradingAccountId = c.TradingAccountId.Value,
        c.StartingBalance,
        Phase = c.Phase.ToString(),
        Status = c.Status.ToString(),
        Breach = c.Breach.ToString(),
        c.CurrentEquity,
        c.PeakEquity,
        c.TradingDaysCount,
        c.ProfitTargetPercent,
        c.MaxDailyLossPercent,
        c.MaxTotalDrawdownPercent,
        DrawdownMode = c.DrawdownMode.ToString(),
        c.MinTradingDays,
        c.SingleStep,
        c.LastEquityAt
    };
}
