namespace Frontend.Components;

/// <summary>
/// Watchdog and queue thresholds handed to the JS player at init time. The defaults live
/// here rather than in radio-player.js so there is a single place to tune them.
/// </summary>
internal sealed record AudioPlayerJsConfig
{
    public required double Volume { get; init; }

    public int StartupTimeoutMs { get; init; } = 20_000;
    public int ProgressTimeoutMs { get; init; } = 30_000;
    public int BufferingTimeoutMs { get; init; } = 90_000;
    public int WatchdogIntervalMs { get; init; } = 500;
    public int ResumeMaxAttempts { get; init; } = 2;
    public int ResumeProbeMs { get; init; } = 1_200;
    public int PreloadLeadMs { get; init; } = 20_000;
    public int StarvedRetryMs { get; init; } = 3_000;
    public int MaxEvents { get; init; } = 40;
}

internal static class AudioPlayerConfig
{
    /// <summary>How many tracks the JS player holds ahead, so it survives a circuit drop.</summary>
    public const int QueueTarget = 3;

    /// <summary>Upper bound on stream-url lookups per refill, so a fully broken playlist cannot spin.</summary>
    public const int MaxLookupsPerRefill = 8;

    private static readonly TimeSpan EmptyPlaylistDelay = TimeSpan.FromSeconds(2);

    public static TimeSpan GetRefillRetryDelay(int retryCount)
    {
        if (retryCount <= 1)
            return EmptyPlaylistDelay;

        var seconds = Math.Min(30, Math.Pow(2, Math.Min(retryCount - 1, 5)));
        return TimeSpan.FromSeconds(seconds);
    }
}
