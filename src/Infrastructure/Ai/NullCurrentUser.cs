using Core;

namespace Infrastructure.Ai;

/// <summary>
/// Fallback <see cref="ICurrentUser"/> for hosts/contexts with no request principal (background services,
/// non-Web hosts). Registered via <c>TryAddScoped</c> so Web's request-scoped implementation wins where it
/// exists; elsewhere it reports "no user", which resolves AI to the deployment-default provider.
/// </summary>
public sealed class NullCurrentUser : ICurrentUser
{
    public UserId? UserId => null;
    public string? RoleName => null;
    public int? RoleRank => null;
    public string? Email => null;
    public bool IsInRole(string roleName) => false;
    public bool IsAtLeast(string roleName) => false;
}
