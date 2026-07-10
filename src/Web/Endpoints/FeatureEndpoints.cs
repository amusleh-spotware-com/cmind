using Core.Constants;
using Core.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Endpoints;

public sealed record FeatureToggleRequest(bool? Enabled);

public sealed record FeatureStateResponse(string Flag, bool Enabled);

public static class FeatureEndpoints
{
    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        // Not itself feature-gated: the owner must always be able to inspect and flip features.
        var g = app.MapGroup("/api/features").RequireAuthorization(AuthPolicies.Owner);

        g.MapGet("/", (IFeatureGate gate) =>
            Results.Ok(gate.Snapshot().Select(kv => new FeatureStateResponse(kv.Key.ToString(), kv.Value))));

        g.MapPut("/{flag}", async (string flag, FeatureToggleRequest req, IFeatureGate gate, CancellationToken ct) =>
        {
            if (!Enum.TryParse<FeatureFlag>(flag, ignoreCase: true, out var parsed))
                return Results.NotFound();
            await gate.SetOverrideAsync(parsed, req.Enabled, ct);
            return Results.Ok(new FeatureStateResponse(parsed.ToString(), gate.IsEnabled(parsed)));
        });

        return app;
    }
}
