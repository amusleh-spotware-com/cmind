using Core;
using Microsoft.Extensions.DependencyInjection;

namespace Nodes;

public static class DependencyInjection
{
    public static IServiceCollection AddNodes(this IServiceCollection services)
    {
        services.AddScoped<INodeScheduler, NodeScheduler>();
        services.AddSingleton<IContainerDispatcher, SshContainerDispatcher>();
        services.AddHostedService<NodeStatsPoller>();
        return services;
    }
}
