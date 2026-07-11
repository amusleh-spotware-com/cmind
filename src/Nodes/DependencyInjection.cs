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
        var appOptions = config.GetSection(AppOptions.SectionName).Get<AppOptions>();
        var features = appOptions?.Features ?? new FeaturesOptions();

        services.AddScoped<INodeScheduler, NodeScheduler>();
        services.AddHttpClient(HttpContainerDispatcher.HttpClientName);
        services.AddSingleton<HttpContainerDispatcher>();
        services.AddSingleton<LocalContainerDispatcher>();
        services.AddSingleton<IContainerDispatcher>(sp => sp.GetRequiredService<HttpContainerDispatcher>());
        services.AddSingleton<IContainerDispatcherFactory, ContainerDispatcherFactory>();
        services.AddHostedService<NodeStatsPoller>();
        services.AddHostedService<NodeHeartbeatMonitor>();
        services.AddHostedService<NodeInstanceReclaimer>();
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

            // Phase 3 execution transparency: when enabled, the host emits per-copy facts to a channel sink
            // that a background drainer persists; otherwise the host gets the no-op sink (unchanged engine).
            var copy = appOptions?.Copy ?? new CopyOptions();
            if (copy.TransparencyEnabled)
            {
                services.AddSingleton<Nodes.CopyTrading.ChannelCopyEventSink>();
                services.AddSingleton<Core.CopyTrading.ICopyEventSink>(
                    sp => sp.GetRequiredService<Nodes.CopyTrading.ChannelCopyEventSink>());
                services.AddHostedService<Nodes.CopyTrading.CopyExecutionDrainer>();
            }
            else
            {
                services.AddSingleton<Core.CopyTrading.ICopyEventSink>(Core.CopyTrading.NullCopyEventSink.Instance);
            }

            // 2b notification routing: on by default (safety alerts). Channel sink + drainer persist the
            // host's operational notifications to the per-owner feed; else the host gets the no-op sink.
            if (copy.NotificationsEnabled)
            {
                services.AddSingleton<Nodes.CopyTrading.ChannelCopyNotificationSink>();
                services.AddSingleton<Core.CopyTrading.ICopyNotificationSink>(
                    sp => sp.GetRequiredService<Nodes.CopyTrading.ChannelCopyNotificationSink>());
                services.AddHostedService<Nodes.CopyTrading.CopyNotificationDrainer>();
            }
            else
            {
                services.AddSingleton<Core.CopyTrading.ICopyNotificationSink>(Core.CopyTrading.NullCopyNotificationSink.Instance);
            }

            // Phase 4 money-manager performance fees (opt-in): the settlement service polls destination
            // equity and settles high-water-mark fees into the CopyFeeAccrual log.
            if (copy.FeesEnabled)
            {
                services.AddSingleton<Core.CopyTrading.ICopyEquityReader, Nodes.CopyTrading.OpenApiCopyEquityReader>();
                services.AddHostedService<Nodes.CopyTrading.CopyFeeSettlementService>();
            }

            services.AddHostedService<Nodes.CopyTrading.CopyEngineSupervisor>();
        }

        if (features.PropFirm)
            services.AddHostedService<Nodes.PropFirm.PropFirmTrackingSupervisor>();

        return services;
    }
}
