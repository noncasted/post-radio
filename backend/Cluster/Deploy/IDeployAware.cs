using Common.Reactive;

namespace Cluster.Deploy;

public interface IDeployAware
{
    Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime);
}