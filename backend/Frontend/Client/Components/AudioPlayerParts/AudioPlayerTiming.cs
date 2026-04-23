namespace Frontend.Client.Components;

internal static class AudioPlayerTiming
{
    public const string ProgressTimeoutReason = "progress-timeout";

    public static readonly TimeSpan EmptyPlaylistDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan PlayFailureDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan WatchdogInterval = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan NormalProgressTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BufferingTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StartupSilencePeriod = TimeSpan.FromSeconds(30);

    public static TimeSpan GetEmptyUrlDelay(int retryCount)
    {
        if (retryCount <= 1)
            return EmptyPlaylistDelay;

        var seconds = Math.Min(30, Math.Pow(2, Math.Min(retryCount - 1, 5)));
        return TimeSpan.FromSeconds(seconds);
    }

    public static bool IsStartupWindow(DateTime componentStartUtc)
    {
        return DateTime.UtcNow - componentStartUtc < StartupSilencePeriod;
    }

    public static bool TryGetWatchdogReason(
        bool isBuffering,
        DateTime? bufferingStartedUtc,
        bool hasReceivedProgress,
        DateTime? loadAndPlayCompletedUtc,
        DateTime lastProgressUtc,
        out string reason)
    {
        var now = DateTime.UtcNow;

        if (isBuffering)
        {
            if (bufferingStartedUtc.HasValue && now - bufferingStartedUtc.Value >= BufferingTimeout)
            {
                reason = "buffering-timeout";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        if (!hasReceivedProgress)
        {
            if (loadAndPlayCompletedUtc.HasValue && now - loadAndPlayCompletedUtc.Value >= StartupTimeout)
            {
                reason = "startup-timeout";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        if (lastProgressUtc != DateTime.MinValue && now - lastProgressUtc >= NormalProgressTimeout)
        {
            reason = ProgressTimeoutReason;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public static double GetSecondsSince(DateTime utc)
    {
        if (utc == DateTime.MinValue)
            return 0;

        return (DateTime.UtcNow - utc).TotalSeconds;
    }

    public static double GetSecondsSince(DateTime? utc)
    {
        if (!utc.HasValue)
            return 0;

        return (DateTime.UtcNow - utc.Value).TotalSeconds;
    }
}