using Core;

namespace Nodes;

public sealed class ContainerDispatcherFactory(
    SshContainerDispatcher ssh,
    LocalContainerDispatcher local) : IContainerDispatcherFactory
{
    public IContainerDispatcher For(Node node) => node is LocalNode ? local : ssh;

    public IContainerDispatcher For(Instance instance)
    {
        if (instance.Node is null) throw new InvalidOperationException("Instance has no node.");
        return For(instance.Node);
    }
}
