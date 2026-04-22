using Common;

namespace Infrastructure;

[GrainState(Table = "configs", State = "side_effects_config", Lookup = "SideEffectsConfig", Key = GrainKeyType.String)]
public class SideEffectsOptions
{
    public int ScanDelay { get; set; } = 5; // ms between worker iterations when work was found
    public int EmptyScanDelay { get; set; } = 2000; // ms between worker iterations when queue is empty
    public int ConcurrentExecutions { get; set; } = 50; // max parallel effects
    public int MaxRetryCount { get; set; } = 5; // max attempts
    public float IncrementalRetryDelay { get; set; } = 30; // seconds * retryCount = delay
    public int StuckCheckIntervalSeconds { get; set; } = 60; // how often to check for stuck entries
    public int StuckThresholdMinutes { get; set; } = 5; // entries older than this are considered stuck
}

public interface ISideEffectsConfig : IAddressableState<SideEffectsOptions>
{
}