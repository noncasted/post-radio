namespace Cluster.Monitoring;

[GenerateSerializer]
public class LiveMatchesData
{
    [Id(0)] public List<LiveMatchEntry> Matches { get; set; } = [];
}

[GenerateSerializer]
public class LiveMatchEntry
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Type { get; set; } = string.Empty;
    [Id(2)] public int PlayerCount { get; set; }
    [Id(3)] public DateTime CreatedAt { get; set; }
    [Id(4)] public string GameMode { get; set; } = string.Empty;
    [Id(5)] public Guid Player1Id { get; set; }
    [Id(6)] public Guid Player2Id { get; set; }
}