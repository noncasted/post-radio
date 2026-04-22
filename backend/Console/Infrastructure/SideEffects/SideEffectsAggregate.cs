using Cluster.Monitoring;

namespace Console.Infrastructure.SideEffects;

public class SideEffectsAggregate
{
    public int QueueCount { get; init; }
    public int ProcessingCount { get; init; }
    public int RetryCount { get; init; }
    public int DeadLetterCount { get; init; }
    public IReadOnlyList<PerServiceSideEffects> Services { get; init; } = [];
    public IReadOnlyList<SideEffectsThroughputEntry> AggregatedHistory { get; init; } = [];
    public IReadOnlyList<SideEffectsRetryEntry> RetryEntries { get; init; } = [];
    public DateTime UpdatedAt { get; init; }
}

public class PerServiceSideEffects
{
    public string ServiceTag { get; init; } = "";
    public Guid ServiceId { get; init; }
    public SideEffectsLiveData Data { get; init; } = new();
}
