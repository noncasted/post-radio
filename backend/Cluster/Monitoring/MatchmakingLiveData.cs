namespace Cluster.Monitoring;

[GenerateSerializer]
public class MatchmakingLiveData
{
    [Id(0)] public int TotalInQueue { get; set; }
    [Id(1)] public List<MatchmakingQueueLiveData> Queues { get; set; } = [];
}

[GenerateSerializer]
public class MatchmakingQueueLiveData
{
    [Id(0)] public string Type { get; set; } = string.Empty;
    [Id(1)] public int Count { get; set; }
    [Id(2)] public double? OldestWaitSeconds { get; set; }
}