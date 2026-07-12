namespace Core.Domain;

/// <summary>
/// Decides which <see cref="OpenApiApplication"/> a user authorizes new accounts against: the deployment
/// shared app when a white-label operator has configured one (shared-mode), otherwise the user's own app.
/// The one place that branches on shared-mode, so endpoints and UI never do it ad-hoc.
/// </summary>
public interface IOpenApiAppResolver
{
    /// <summary>The app this user authorizes against — shared row in shared-mode, else the user's own (or null).</summary>
    Task<OpenApiApplication?> ResolveForUserAsync(UserId userId, CancellationToken ct);

    /// <summary>True when a deployment shared app exists — drives UI gating and per-user CRUD blocking.</summary>
    Task<bool> IsSharedModeAsync(CancellationToken ct);
}
