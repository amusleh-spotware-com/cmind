using Core;
using Core.Agent;
using Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nodes.Agent;
using Nodes.Alerts;
using Nodes.PropGuard;

namespace Nodes;

public static class DependencyInjection
{
    public static IServiceCollection AddNodes(this IServiceCollection services, IConfiguration config)
    {
        var features = config.GetSection(AppOptions.SectionName).Get<AppOptions>()?.Features ?? new FeaturesOptions();

        services.AddScoped<INodeScheduler, NodeScheduler>();
        services.AddHttpClient(HttpContainerDispatcher.HttpClientName);
        services.AddSingleton<HttpContainerDispatcher>();
        services.AddSingleton<LocalContainerDispatcher>();
        services.AddSingleton<IContainerDispatcher>(sp => sp.GetRequiredService<HttpContainerDispatcher>());
        services.AddSingleton<IContainerDispatcherFactory, ContainerDispatcherFactory>();
        services.AddHostedService<NodeStatsPoller>();
        services.AddHostedService<NodeHeartbeatMonitor>();
        services.AddHostedService<BacktestCompletionPoller>();
        services.AddHostedService<RunCompletionPoller>();
        services.AddScoped<IAgentExecutor, AgentExecutor>();

        if (features.Ai) services.AddHostedService<AiRiskGuard>();
        if (features.PortfolioAgent) services.AddHostedService<PortfolioAgentService>();
        if (features.Alerts) services.AddHostedService<AlertEvaluator>();
        if (features.PropGuard) services.AddHostedService<PropGuardService>();
        if (features.CopyTrading)
        {
            services.AddHostedService<Nodes.CopyTrading.OpenApiTokenRefreshService>();
            services.AddHostedService<Nodes.CopyTrading.CopyEngineSupervisor>();
        }

        if (features.PropFirm)
            services.AddHostedService<Nodes.PropFirm.PropFirmTrackingSupervisor>();

        return services;
    }
}
