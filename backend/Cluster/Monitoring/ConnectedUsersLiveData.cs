namespace Cluster.Monitoring;

[GenerateSerializer]
public class ConnectedUsersLiveData
{
    [Id(0)] public int Count { get; set; }
    [Id(1)] public List<ConnectedUserEntry> Users { get; set; } = [];
}

[GenerateSerializer]
public class ConnectedUserEntry
{
    [Id(0)] public Guid UserId { get; set; }
}