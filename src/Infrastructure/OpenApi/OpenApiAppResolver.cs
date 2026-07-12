using Core;
using Core.Domain;

namespace Infrastructure.OpenApi;

public sealed class OpenApiAppResolver(IOpenApiApplicationRepository apps) : IOpenApiAppResolver
{
    public async Task<OpenApiApplication?> ResolveForUserAsync(UserId userId, CancellationToken ct)
        => await apps.GetSharedAsync(ct) ?? await apps.GetByUserAsync(userId, ct);

    public async Task<bool> IsSharedModeAsync(CancellationToken ct)
        => await apps.GetSharedAsync(ct) is not null;
}
