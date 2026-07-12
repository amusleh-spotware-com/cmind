using Core.Constants;
using Core.Domain;
using Core.WhiteLabel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Endpoints;

public sealed record SetWhiteLabelRequest(string? Value);

public sealed record WhiteLabelOptionResponse(
    string Key,
    string ConfigKey,
    string Kind,
    string Category,
    string Label,
    string Description,
    bool OwnerEditable,
    bool IsSecret,
    bool IsFeatureFlag,
    string? DelegatedToSection,
    IReadOnlyList<string> EnumValues,
    string? Value,
    bool HasValue,
    string Source,
    bool HasOverride);

/// <summary>
/// Owner-only white-label deployment settings API. Not feature-gated — the owner must always be able to
/// inspect and change deployment options. Every white-label option the deployment configures is settable here
/// at runtime; the override wins over configuration and takes effect without a redeploy.
/// </summary>
public static class WhiteLabelSettingsEndpoints
{
    public static IEndpointRouteBuilder MapWhiteLabelSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whitelabel").RequireAuthorization(AuthPolicies.Owner);

        group.MapGet("/", async (IWhiteLabelSettings settings, CancellationToken ct) =>
        {
            var snapshot = await settings.SnapshotAsync(ct);
            return Results.Ok(snapshot.Select(Map));
        });

        group.MapPut("/{key}", async (string key, SetWhiteLabelRequest req, IWhiteLabelSettings settings, CancellationToken ct) =>
        {
            try
            {
                await settings.SetOverrideAsync(key, req.Value, ct);
                return Results.NoContent();
            }
            catch (DomainException ex)
            {
                return ex.Code == DomainErrors.WhiteLabelOptionUnknown
                    ? Results.NotFound()
                    : Results.BadRequest(new { error = ex.Code });
            }
        });

        group.MapDelete("/{key}", async (string key, IWhiteLabelSettings settings, CancellationToken ct) =>
        {
            try
            {
                await settings.ClearOverrideAsync(key, ct);
                return Results.NoContent();
            }
            catch (DomainException ex)
            {
                return ex.Code == DomainErrors.WhiteLabelOptionUnknown
                    ? Results.NotFound()
                    : Results.BadRequest(new { error = ex.Code });
            }
        });

        group.MapPost("/reset", async (IWhiteLabelSettings settings, CancellationToken ct) =>
        {
            await settings.ClearAllOverridesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }

    private static WhiteLabelOptionResponse Map(WhiteLabelEffectiveValue effective)
    {
        var option = effective.Option;
        var enumValues = option.EnumType is null ? [] : System.Enum.GetNames(option.EnumType);
        return new WhiteLabelOptionResponse(
            option.Key,
            option.ConfigKey,
            option.Kind.ToString(),
            option.Category.ToString(),
            option.Label,
            option.Description,
            option.OwnerEditable,
            option.IsSecret,
            option.IsFeatureFlag,
            option.DelegatedToSection,
            enumValues,
            effective.Value,
            effective.HasValue,
            effective.Source.ToString(),
            effective.HasOverride);
    }
}
