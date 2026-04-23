namespace Frontend.Shared;

public class PlaylistDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
}

public class SongDto
{
    public required long Id { get; init; }
    public required string Author { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required IReadOnlyList<Guid> Playlists { get; init; }
    public required DateTime AddDate { get; init; }
    public long? DurationMs { get; init; }
    public bool IsValid { get; init; } = true;
}

public static class PlayableTrackPolicy
{
    public const long MinimumPlayableDurationMs = 31_000;

    public static bool IsPlayable(SongDto song)
    {
        return song.IsValid && IsValidDurationMs(song.DurationMs);
    }

    public static bool IsValidDurationMs(long? durationMs)
    {
        return durationMs is >= MinimumPlayableDurationMs;
    }
}

public class ImagesCountDto
{
    public required int Count { get; init; }
}

public class FrontendOptionsDto
{
    public required float BaseVolume { get; init; }
    public required float MaxVolume { get; init; }
    public required int ImageSwitchIntervalMs { get; init; }
    public required int ImageFadeMs { get; init; }
}
