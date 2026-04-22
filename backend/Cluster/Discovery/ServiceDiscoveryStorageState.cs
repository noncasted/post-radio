using Common;
using Infrastructure.State;

namespace Cluster.Discovery;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "service_discovery", Lookup = "ServiceDiscovery", Key = GrainKeyType.Guid)]
public class ServiceDiscoveryStorageState : IStateValue
{
    [Id(0)] public Dictionary<Guid, IServiceOverview> Members { get; set; } = new();

    public int Version => 0;
}