using Core;
using Nodes.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Nodes;

public static class DependencyInjection
{
    public static IServiceCollection AddCtwNodes(this IServiceCollection services)
    {
        services.AddScoped<INodeScheduler, NodeScheduler>();
        services.AddSingleton<IContainerDispatcher, SshContainerDispatcher>();
        services.AddScoped<CBotBuilder>();
        services.AddHostedService<NodeStatsPoller>();
        return services;
    }
}
