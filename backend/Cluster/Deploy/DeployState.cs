using Common;
using Infrastructure.State;

namespace Cluster.Deploy;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "deploy", Lookup = "Deploy", Key = GrainKeyType.Guid)]
public class DeployState : IStateValue
{
    [Id(0)] public Guid DeployId { get; set; }
    [Id(1)] public DateTime LastHeartbeat { get; set; }
    [Id(2)] public bool CoordinatorReady { get; set; }

    public int Version => 0;
}