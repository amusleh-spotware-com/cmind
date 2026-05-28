using System.Security.Claims;
using Core;
using Microsoft.AspNetCore.Http;

namespace Web.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private static readonly Dictionary<string, int> Ranks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Owner"] = 0,
        ["Admin"] = 1,
        ["User"] = 2,
        ["Viewer"] = 3
    };

    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public UserId? UserId
    {
        get
        {
            var s = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(s, out var g) ? Core.UserId.From(g) : null;
        }
    }

    public string? RoleName => User?.FindFirst(ClaimTypes.Role)?.Value;

    public int? RoleRank => RoleName is { } n && Ranks.TryGetValue(n, out var r) ? r : null;

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsInRole(string roleName) =>
        string.Equals(RoleName, roleName, StringComparison.OrdinalIgnoreCase);

    public bool IsAtLeast(string roleName) =>
        RoleRank is { } mine && Ranks.TryGetValue(roleName, out var target) && mine <= target;
}
