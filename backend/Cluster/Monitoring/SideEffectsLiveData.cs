namespace Cluster.Monitoring;

[GenerateSerializer]
public class SideEffectsSnapshotRequest { }

[GenerateSerializer]
public class SideEffectsSnapshotResponse
{
    [Id(0)] public string ServiceTag { get; set; } = "";
    [Id(1)] public Guid ServiceId { get; set; }
    [Id(2)] public SideEffectsLiveData Data { get; set; } = new();
}

[GenerateSerializer]
public class SideEffectsLiveData
{
    [Id(0)] public int QueueCount { get; set; }
    [Id(1)] public int ProcessingCount { get; set; }
    [Id(2)] public int RetryCount { get; set; }
    [Id(3)] public int DeadLetterCount { get; set; }
    [Id(4)] public List<SideEffectsThroughputEntry> ThroughputHistory { get; set; } = [];
    [Id(5)] public List<SideEffectsRetryEntry> RetryEntries { get; set; } = [];
}

[GenerateSerializer]
public class SideEffectsThroughputEntry
{
    [Id(0)] public DateTime Timestamp { get; set; }
    [Id(1)] public long Processed { get; set; }
    [Id(2)] public long Failed { get; set; }
}

[GenerateSerializer]
public class SideEffectsRetryEntry
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string TypeName { get; set; } = string.Empty;
    [Id(2)] public int RetryCount { get; set; }
    [Id(3)] public DateTime RetryAfter { get; set; }
    [Id(4)] public DateTime CreatedAt { get; set; }
}