using Azure.Monitor.OpenTelemetry.Exporter;
using Core;
using Core.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Infrastructure.Observability;

/// <summary>
/// Shared OpenTelemetry metrics+traces pipeline for the HTTP services (Web, MCP). Exports over OTLP
/// when OTEL_EXPORTER_OTLP_ENDPOINT is set (any collector: ADOT on AWS, otel-collector on K8s) and
/// natively to Azure Monitor / Application Insights when APPLICATIONINSIGHTS_CONNECTION_STRING is
/// set — both, either, or neither, with no code change. Resource attributes (service name/version/
/// namespace + deployment environment) are shared with the Serilog pipeline so logs, traces, and
/// metrics group identically in every backend.
/// </summary>
public static class OpenTelemetryConfigurator
{
    public static IServiceCollection AddAppTelemetry(
        this IServiceCollection services, IConfiguration configuration,
        string serviceName, string environmentName)
    {
        var otlpEndpoint = configuration[ObservabilityDefaults.OtlpEndpointKey];
        var azureMonitorConnectionString = configuration[ObservabilityDefaults.AzureMonitorConnectionStringKey];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName,
                    serviceNamespace: ObservabilityDefaults.ServiceNamespace,
                    serviceVersion: VersionInfo.Product)
                .AddAttributes([
                    new KeyValuePair<string, object>(
                        ObservabilityDefaults.DeploymentEnvironmentProperty, environmentName)
                ]))
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(ObservabilityDefaults.CopyMeterName);
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    m.AddOtlpExporter();
                if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
                    m.AddAzureMonitorMetricExporter(o => o.ConnectionString = azureMonitorConnectionString);
            })
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    t.AddOtlpExporter();
                if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
                    t.AddAzureMonitorTraceExporter(o => o.ConnectionString = azureMonitorConnectionString);
            });
        return services;
    }
}
