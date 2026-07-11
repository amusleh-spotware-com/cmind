using Core;
using Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Endpoints;

public static class ConsentEndpointExtensions
{
    /// <summary>
    /// Blocks the endpoint with 403 when a published legal document of <paramref name="type"/> exists and the
    /// current user has not consented to its active version. When no such document is published, the action is
    /// allowed (there is nothing to consent to yet).
    /// </summary>
    public static TBuilder RequireConsent<TBuilder>(this TBuilder builder, LegalDocumentType type)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            if (services.GetRequiredService<ICurrentUser>().UserId is not { } uid)
                return Results.Unauthorized();

            var ct = context.HttpContext.RequestAborted;
            var active = await services.GetRequiredService<ILegalDocumentRepository>().GetActiveAsync(type, ct);
            if (active is not null &&
                !await services.GetRequiredService<IConsentRepository>().HasConsentAsync(uid, type, active.Version, ct))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    title: "consent_required", detail: type.ToString());

            return await next(context);
        });
        return builder;
    }
}
