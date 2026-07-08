using Core.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Infrastructure.Observability;

/// <summary>
/// Structured logging for the distributed app: compact JSON to stdout (container/K8s log
/// collectors ingest it directly) plus an OTLP sink when OTEL_EXPORTER_OTLP_ENDPOINT is set,
/// which carries trace/span correlation from the ambient Activity. appsettings "Serilog"
/// overrides levels via ReadFrom.Configuration.
/// </summary>
public static class SerilogConfigurator
{
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.AddSerilog((sp, lc) =>
        {
            lc.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty(ObservabilityDefaults.ServiceNameProperty, serviceName)
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(sp)
                .WriteTo.Console(new RenderedCompactJsonFormatter());

            var otlpEndpoint = configuration[ObservabilityDefaults.OtlpEndpointKey];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                lc.WriteTo.OpenTelemetry(o =>
                {
                    o.Endpoint = otlpEndpoint;
                    o.ResourceAttributes = new Dictionary<string, object>
                    {
                        [ObservabilityDefaults.ServiceNameProperty] = serviceName
                    };
                });
            }
        });
        return services;
    }
}
