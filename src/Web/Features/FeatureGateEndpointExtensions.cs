using Core.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Endpoints;

public static class FeatureGateEndpointExtensions
{
    /// <summary>
    /// Short-circuits the endpoint with 404 when <paramref name="flag"/> is disabled for the deployment.
    /// Evaluated per request through <see cref="IFeatureGate"/>, so a feature can be enabled at runtime
    /// without remapping routes.
    /// </summary>
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, FeatureFlag flag)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var gate = context.HttpContext.RequestServices.GetRequiredService<IFeatureGate>();
            return gate.IsEnabled(flag) ? await next(context) : Results.NotFound();
        });
        return builder;
    }
}
