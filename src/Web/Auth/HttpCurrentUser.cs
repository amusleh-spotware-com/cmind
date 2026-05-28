using System.Security.Claims;
using Core;
using Microsoft.AspNetCore.Http;

namespace Web.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var s = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(s, out var g) ? g : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var s = User?.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(s)) return null;
            try { return UserRole.FromName(s); } catch { return null; }
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsInRole(UserRole role) => Role == role;
    public bool IsAtLeast(UserRole role) => Role is { } r && r.Rank <= role.Rank;
}
