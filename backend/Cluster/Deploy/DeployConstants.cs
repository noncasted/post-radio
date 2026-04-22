using Cluster.Discovery;

namespace Cluster.Deploy;

public static class DeployConstants
{
    public static readonly IReadOnlyList<ServiceTag> RequiredServices = new[]
    {
        ServiceTag.Coordinator,
        ServiceTag.Meta,
        ServiceTag.Silo,
        ServiceTag.Console,
    };
}
