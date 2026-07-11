using System.ComponentModel;
using System.Security.Claims;
using Core;
using Core.Ai;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

[McpServerToolType]
public sealed class CopyTools(DataContext db, IHttpContextAccessor http, IAiFeatureService ai)
{
    private UserId? CurrentUserId => Guid.TryParse(
        http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? UserId.From(g) : null;

    [McpServerTool, Description("List the user's copy-trading profiles with status and destination count.")]
    public async Task<object> ListCopyProfiles()
    {
        if (CurrentUserId is not { } uid) return Array.Empty<object>();
        var profiles = await db.CopyProfiles.Include(p => p.Destinations)
            .Where(p => p.UserId == uid).ToListAsync();
        return profiles.Select(p => new
        {
            p.Id,
            p.Name,
            Status = p.Status.ToString(),
            SourceAccountId = p.SourceAccountId.Value,
            DestinationCount = p.Destinations.Count
        }).ToList();
    }

    [McpServerTool, Description(
        "Recommend safe copy-trading destination settings (as JSON) for a follower's risk profile. " +
        "Returns an AI suggestion; does not create anything.")]
    public async Task<object> RecommendCopyProfile(
        [Description("Follower risk profile, e.g. conservative / balanced / aggressive")] string riskProfile,
        [Description("Description of the source (master) account or strategy being copied")] string sourceDescription)
    {
        var result = await ai.RecommendCopyProfileAsync(riskProfile, sourceDescription, CancellationToken.None);
        return new { success = result.Success, recommendation = result.Text, error = result.Error };
    }

    [McpServerTool, Description("Start a copy-trading profile.")]
    public Task<object?> StartCopyProfile([Description("Copy profile ID")] Guid profileId)
        => TransitionAsync(profileId, p => p.Start());

    [McpServerTool, Description("Pause a running copy-trading profile.")]
    public Task<object?> PauseCopyProfile([Description("Copy profile ID")] Guid profileId)
        => TransitionAsync(profileId, p => p.Pause());

    [McpServerTool, Description("Stop a copy-trading profile.")]
    public Task<object?> StopCopyProfile([Description("Copy profile ID")] Guid profileId)
        => TransitionAsync(profileId, p => p.Stop());

    private async Task<object?> TransitionAsync(Guid profileId, Action<CopyProfile> transition)
    {
        if (CurrentUserId is not { } uid) return null;
        var id = CopyProfileId.From(profileId);
        var profile = await db.CopyProfiles.FirstOrDefaultAsync(p => p.Id == id && p.UserId == uid);
        if (profile is null) return null;
        transition(profile);
        await db.SaveChangesAsync();
        return new { profile.Id, Status = profile.Status.ToString() };
    }
}
