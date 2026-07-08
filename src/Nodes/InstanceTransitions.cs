using Core;

namespace Nodes;

public static class InstanceTransitions
{
    public static StoppedRunInstance StoppedFrom(RunningRunInstance instance, DateTimeOffset stoppedAt) => new()
    {
        UserId = instance.UserId,
        CBotId = instance.CBotId,
        TradingAccountId = instance.TradingAccountId,
        NodeId = instance.NodeId,
        DockerImageTag = instance.DockerImageTag,
        Symbol = instance.Symbol,
        Timeframe = instance.Timeframe,
        ParamSetId = instance.ParamSetId,
        ContainerId = instance.ContainerId,
        StartedAt = instance.StartedAt,
        StoppedAt = stoppedAt,
        DataDirSubPath = instance.DataDirSubPath
    };
}
