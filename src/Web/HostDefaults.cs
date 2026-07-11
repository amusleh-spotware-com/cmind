using Core.Constants;
using Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Web;

internal static class HostDefaults
{
    public static TBuilder AddObservabilityDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddStructuredLogging(
            builder.Configuration, ObservabilityDefaults.WebServiceName, builder.Environment.EnvironmentName);
        builder.Services.AddAppTelemetry(
            builder.Configuration, ObservabilityDefaults.WebServiceName, builder.Environment.EnvironmentName);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromMinutes(10);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(15);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(30);
            });
            http.AddServiceDiscovery();
        });
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [HealthEndpoints.LiveTag]);
        return builder;
    }

    public static WebApplication MapHostHealthEndpoints(this WebApplication app)
    {
        // Mapped in all environments so container/K8s probes work in production.
        // /health = readiness (all checks incl. database); /alive = liveness (process only).
        app.MapHealthChecks(HealthEndpoints.Health).AllowAnonymous();
        app.MapHealthChecks(HealthEndpoints.Alive, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthEndpoints.LiveTag)
        }).AllowAnonymous();
        return app;
    }
}
