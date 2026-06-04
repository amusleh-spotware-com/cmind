using Core;
using Microsoft.Extensions.DependencyInjection;

namespace Nodes;

public static class DependencyInjection
{
    public static IServiceCollection AddNodes(this IServiceCollection services)
    {
        services.AddScoped<INodeScheduler, NodeScheduler>();
        services.AddSingleton<SshContainerDispatcher>();
        services.AddSingleton<LocalContainerDispatcher>();
        services.AddSingleton<IContainerDispatcher>(sp => sp.GetRequiredService<SshContainerDispatcher>());
        services.AddSingleton<IContainerDispatcherFactory, ContainerDispatcherFactory>();
        services.AddHostedService<NodeStatsPoller>();
        return services;
    }
}
