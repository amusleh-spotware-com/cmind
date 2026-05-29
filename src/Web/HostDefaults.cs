using Core.Constants;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Web;

internal static class HostDefaults
{
    public static TBuilder AddObservabilityDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });
        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Logging.AddOpenTelemetry(o => o.AddOtlpExporter());
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
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpoints.Health);
            app.MapHealthChecks(HealthEndpoints.Alive, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains(HealthEndpoints.LiveTag)
            });
        }
        return app;
    }
}
