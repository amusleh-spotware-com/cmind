using Core.Constants;
using Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Web;

internal static class HostDefaults
{
    public static TBuilder AddObservabilityDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddStructuredLogging(builder.Configuration, ObservabilityDefaults.WebServiceName);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration[ObservabilityDefaults.OtlpEndpointKey]))
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(m => m.AddOtlpExporter())
                .WithTracing(t => t.AddOtlpExporter());
        }

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
