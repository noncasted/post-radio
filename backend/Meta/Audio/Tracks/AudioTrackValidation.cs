namespace Meta.Audio;

public static class AudioTrackValidation
{
    public static readonly TimeSpan MinimumPlayableDuration = TimeSpan.FromSeconds(31);

    public static bool IsPlayableAudio(bool isLoaded, bool isValid, long? durationMs)
    {
        return isLoaded && isValid && IsValidLocalDurationMs(durationMs);
    }

    public static bool IsValidLocalDuration(TimeSpan? duration)
    {
        return duration.HasValue && duration.Value >= MinimumPlayableDuration;
    }

    public static bool IsValidLocalDurationMs(long? durationMs)
    {
        return durationMs.HasValue && TimeSpan.FromMilliseconds(durationMs.Value) >= MinimumPlayableDuration;
    }
}
