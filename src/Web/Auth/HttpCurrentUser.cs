using System.Security.Claims;
using Core;
using Microsoft.AspNetCore.Http;

namespace Web.Auth;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

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
            return Enum.TryParse<UserRole>(s, out var r) ? r : null;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsInRole(UserRole role) => Role == role;
    public bool IsAtLeast(UserRole role) => Role is { } r && (int)r <= (int)role;
}
