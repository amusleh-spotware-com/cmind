using Core;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddHostedService<BacktestCompletionPoller>();
        services.AddHostedService<RunCompletionPoller>();
        services.AddHostedService<AiRiskGuard>();
        return services;
    }
}
