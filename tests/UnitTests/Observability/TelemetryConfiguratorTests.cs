using Core.Constants;
using FluentAssertions;
using Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace UnitTests.Observability;

/// <summary>
/// Guards the cloud observability wiring without any cloud infrastructure: the shared OTLP + Azure Monitor
/// pipeline must build a working tracer/meter provider whether the AWS/K8s OTLP endpoint, the Azure
/// Application Insights connection string, both, or neither are configured. Regression guard for the
/// deployment paths (cloud-aws / cloud-azure) that a live cluster is not needed to exercise.
/// </summary>
public sealed class TelemetryConfiguratorTests
{
    // A syntactically valid but inert Application Insights connection string — parsed at exporter
    // construction, never dialed (no export happens when we merely build the provider).
    private const string FakeAppInsights =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/";

    private static ServiceProvider Build(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppTelemetry(configuration, ObservabilityDefaults.WebServiceName, "Production");
        return services.BuildServiceProvider();
    }

    [Fact]
    public void No_exporter_config_still_builds_a_working_pipeline()
    {
        using var provider = Build([]);

        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
        provider.GetRequiredService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void Otlp_endpoint_builds_the_pipeline_for_aws_adot_and_k8s_collector()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [ObservabilityDefaults.OtlpEndpointKey] = "http://localhost:4317"
        });

        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
        provider.GetRequiredService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void Azure_monitor_connection_string_builds_the_app_insights_pipeline()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [ObservabilityDefaults.AzureMonitorConnectionStringKey] = FakeAppInsights
        });

        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
        provider.GetRequiredService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void Both_backends_configured_at_once_build_a_single_pipeline()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [ObservabilityDefaults.OtlpEndpointKey] = "http://localhost:4317",
            [ObservabilityDefaults.AzureMonitorConnectionStringKey] = FakeAppInsights
        });

        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
        provider.GetRequiredService<MeterProvider>().Should().NotBeNull();
    }
}
