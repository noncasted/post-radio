namespace Meta.Audio;

public static class AudioTrackValidation
{
    public static readonly TimeSpan MinimumPlayableDuration = TimeSpan.FromSeconds(31);

    public static bool IsPlayableAudio(bool isLoaded, bool isValid, long? durationMs)
    {
        return isLoaded && isValid && IsValidLocalDurationMs(durationMs);
    }

    // Anything that is not playable right now is worth downloading again: never
    // fetched, marked invalid, or already stored but shorter than a real track
    // (a saved 30s preview looks "loaded" but is useless).
    public static bool IsLoadCandidate(bool isLoaded, bool isValid, long? durationMs)
    {
        return !IsPlayableAudio(isLoaded, isValid, durationMs);
    }

    public static bool IsInvalidPlaybackCandidate(bool isLoaded, bool isValid, long? durationMs)
    {
        return !isValid || (isLoaded && !IsValidLocalDurationMs(durationMs));
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
