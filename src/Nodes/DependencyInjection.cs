using Core;
using Core.Agent;
using Microsoft.Extensions.DependencyInjection;
using Nodes.Agent;
using Nodes.Alerts;
using Nodes.PropGuard;

namespace Nodes;

public static class DependencyInjection
{
    public static IServiceCollection AddNodes(this IServiceCollection services)
    {
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
        services.AddHostedService<AiRiskGuard>();
        services.AddScoped<IAgentExecutor, AgentExecutor>();
        services.AddHostedService<PortfolioAgentService>();
        services.AddHostedService<AlertEvaluator>();
        services.AddHostedService<PropGuardService>();
        services.AddHostedService<Nodes.CopyTrading.OpenApiTokenRefreshService>();
        services.AddHostedService<Nodes.CopyTrading.CopyEngineSupervisor>();
        return services;
    }
}
