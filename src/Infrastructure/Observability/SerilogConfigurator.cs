using Core;
using Core.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Infrastructure.Observability;

/// <summary>
/// Structured logging for the distributed app: compact JSON to stdout (container/K8s log
/// collectors ingest it directly) plus an OTLP sink when OTEL_EXPORTER_OTLP_ENDPOINT is set.
/// Every event is stamped with the OpenTelemetry resource identity (service name/version/
/// namespace, deployment environment) and the ambient trace/span id (<see cref="ActivityEnricher"/>)
/// so CloudWatch Logs Insights and Azure Log Analytics correlate logs to traces with or without a
/// collector. appsettings "Serilog" overrides levels via ReadFrom.Configuration.
/// </summary>
public static class SerilogConfigurator
{
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services, IConfiguration configuration,
        string serviceName, string environmentName)
    {
        var serviceVersion = VersionInfo.Product;
        services.AddSerilog((sp, lc) =>
        {
            lc.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.With<ActivityEnricher>()
                .Enrich.WithProperty(ObservabilityDefaults.ServiceNameProperty, serviceName)
                .Enrich.WithProperty(ObservabilityDefaults.ServiceVersionProperty, serviceVersion)
                .Enrich.WithProperty(ObservabilityDefaults.ServiceNamespaceProperty, ObservabilityDefaults.ServiceNamespace)
                .Enrich.WithProperty(ObservabilityDefaults.DeploymentEnvironmentProperty, environmentName)
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
                        [ObservabilityDefaults.ServiceNameProperty] = serviceName,
                        [ObservabilityDefaults.ServiceVersionProperty] = serviceVersion,
                        [ObservabilityDefaults.ServiceNamespaceProperty] = ObservabilityDefaults.ServiceNamespace,
                        [ObservabilityDefaults.DeploymentEnvironmentProperty] = environmentName
                    };
                });
            }
        });
        return services;
    }
}
