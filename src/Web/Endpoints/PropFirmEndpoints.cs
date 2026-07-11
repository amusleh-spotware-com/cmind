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
    bool SingleStep,
    ChallengeKind Kind = ChallengeKind.Custom,
    DailyLossBasis DailyLossBasis = DailyLossBasis.Equity,
    decimal TrailingThresholdAmount = 0,
    decimal TrailingLockThreshold = 0,
    double? ConsistencyMaxDayProfitSharePercent = null,
    int? MaxCalendarDays = null,
    int? MaxInactivityDays = null,
    int? MaxOpenPositions = null,
    bool AllowWeekendHolding = true,
    bool AllowNewsTrading = true);

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

        g.MapGet("/templates", () => Results.Ok(ChallengeTemplates.All.Select(kind =>
        {
            var rules = ChallengeTemplates.For(kind);
            return new
            {
                Kind = kind.ToString(),
                ProfitTargetPercent = rules.ProfitTarget.Value,
                MaxDailyLossPercent = rules.MaxDailyLoss.Value,
                MaxTotalDrawdownPercent = rules.MaxTotalDrawdown.Value,
                DrawdownMode = rules.DrawdownMode.ToString(),
                rules.MinTradingDays.Value,
                rules.SingleStep,
                DailyLossBasis = rules.DailyLossBasis.ToString()
            };
        })));

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
                    req.SingleStep)
                {
                    Kind = req.Kind,
                    DailyLossBasis = req.DailyLossBasis,
                    TrailingThresholdAmount = req.TrailingThresholdAmount,
                    TrailingLockThreshold = req.TrailingLockThreshold,
                    ConsistencyMaxDayProfitSharePercent = req.ConsistencyMaxDayProfitSharePercent,
                    MaxCalendarDays = req.MaxCalendarDays,
                    MaxInactivityDays = req.MaxInactivityDays,
                    MaxOpenPositions = req.MaxOpenPositions,
                    AllowWeekendHolding = req.AllowWeekendHolding,
                    AllowNewsTrading = req.AllowNewsTrading
                };
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

        g.MapPost("/challenges/{id:guid}/start", async (Guid id, IPropFirmChallengeRepository repo,
            ICurrentUser u, CancellationToken ct) =>
        {
            var challenge = await repo.GetByIdAsync(PropFirmChallengeId.From(id), u.UserId!.Value, ct);
            if (challenge is null) return Results.NotFound();
            try
            {
                challenge.Resume();
                await repo.SaveChangesAsync(ct);
                return Results.Ok(Project(challenge));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
        });

        g.MapPost("/challenges/{id:guid}/stop", async (Guid id, IPropFirmChallengeRepository repo,
            ICurrentUser u, CancellationToken ct) =>
        {
            var challenge = await repo.GetByIdAsync(PropFirmChallengeId.From(id), u.UserId!.Value, ct);
            if (challenge is null) return Results.NotFound();
            try
            {
                challenge.Stop();
                await repo.SaveChangesAsync(ct);
                return Results.Ok(Project(challenge));
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
        Kind = c.Kind.ToString(),
        c.CurrentEquity,
        c.CurrentBalance,
        c.PeakEquity,
        c.TradingDaysCount,
        c.ProfitTargetPercent,
        c.MaxDailyLossPercent,
        c.MaxTotalDrawdownPercent,
        DrawdownMode = c.DrawdownMode.ToString(),
        DailyLossBasis = c.DailyLossBasis.ToString(),
        c.ConsistencyMaxDayProfitSharePercent,
        c.MaxCalendarDays,
        c.MaxOpenPositions,
        c.AllowWeekendHolding,
        c.AllowNewsTrading,
        c.MinTradingDays,
        c.SingleStep,
        c.AssignedNode,
        c.LastEquityAt
    };
}
