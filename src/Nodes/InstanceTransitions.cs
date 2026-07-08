using Core;

namespace Nodes;

public static class InstanceTransitions
{
    public static StoppedRunInstance StoppedFrom(RunningRunInstance instance, DateTimeOffset stoppedAt) =>
        instance.ToStopped(stoppedAt);
}
