using System.ComponentModel;
using System.Security.Claims;
using Core;
using Core.Domain;
using Core.PropFirm;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

[McpServerToolType]
public sealed class PropFirmTools(DataContext db, IHttpContextAccessor http, TimeProvider clock)
{
    private UserId? CurrentUserId => Guid.TryParse(
        http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? UserId.From(g) : null;

    [McpServerTool, Description("List the user's prop-firm challenges with kind, phase, status and live equity.")]
    public async Task<object> ListPropFirmChallenges()
    {
        if (CurrentUserId is not { } uid) return Array.Empty<object>();
        var challenges = await db.PropFirmChallenges.Where(c => c.UserId == uid).ToListAsync();
        return challenges.Select(Project).ToList();
    }

    [McpServerTool, Description(
        "Create a prop-firm challenge from a template (OnePhase, TwoPhase, ThreePhase, InstantFunding) " +
        "for a trading account with a starting balance.")]
    public async Task<object?> CreatePropFirmChallenge(
        [Description("Challenge name")] string name,
        [Description("Trading account ID")] Guid tradingAccountId,
        [Description("Starting balance in account currency")] decimal startingBalance,
        [Description("Template: OnePhase, TwoPhase, ThreePhase, InstantFunding")] string template = "TwoPhase")
    {
        if (CurrentUserId is not { } uid) return null;
        if (!Enum.TryParse<ChallengeKind>(template, ignoreCase: true, out var kind)) kind = ChallengeKind.TwoPhase;

        try
        {
            var challenge = PropFirmChallenge.Create(uid, TradingAccountId.From(tradingAccountId), name,
                new Money(startingBalance), ChallengeTemplates.For(kind));
            db.PropFirmChallenges.Add(challenge);
            await db.SaveChangesAsync();
            return Project(challenge);
        }
        catch (DomainException ex)
        {
            return new { error = ex.Code };
        }
    }

    [McpServerTool, Description("Record an equity snapshot for a challenge and re-evaluate its rules.")]
    public async Task<object?> RecordPropFirmEquity(
        [Description("Challenge ID")] Guid challengeId,
        [Description("Current account equity")] decimal equity)
    {
        if (CurrentUserId is not { } uid) return null;
        var challenge = await Find(challengeId, uid);
        if (challenge is null) return null;
        try
        {
            challenge.RecordEquity(new Money(equity), clock.GetUtcNow());
            await db.SaveChangesAsync();
            return Project(challenge);
        }
        catch (DomainException ex)
        {
            return new { error = ex.Code };
        }
    }

    [McpServerTool, Description("Stop tracking a prop-firm challenge (Active -> Stopped).")]
    public Task<object?> StopPropFirmChallenge([Description("Challenge ID")] Guid challengeId)
        => TransitionAsync(challengeId, c => c.Stop());

    [McpServerTool, Description("Resume a stopped prop-firm challenge (Stopped -> Active).")]
    public Task<object?> StartPropFirmChallenge([Description("Challenge ID")] Guid challengeId)
        => TransitionAsync(challengeId, c => c.Resume());

    private async Task<object?> TransitionAsync(Guid challengeId, Action<PropFirmChallenge> transition)
    {
        if (CurrentUserId is not { } uid) return null;
        var challenge = await Find(challengeId, uid);
        if (challenge is null) return null;
        try
        {
            transition(challenge);
            await db.SaveChangesAsync();
            return Project(challenge);
        }
        catch (DomainException ex)
        {
            return new { error = ex.Code };
        }
    }

    private Task<PropFirmChallenge?> Find(Guid challengeId, UserId uid)
    {
        var id = PropFirmChallengeId.From(challengeId);
        return db.PropFirmChallenges.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid);
    }

    private static object Project(PropFirmChallenge c) => new
    {
        c.Id,
        c.Name,
        Kind = c.Kind.ToString(),
        Phase = c.Phase.ToString(),
        Status = c.Status.ToString(),
        Breach = c.Breach.ToString(),
        c.CurrentEquity,
        c.PeakEquity,
        c.TradingDaysCount
    };
}
