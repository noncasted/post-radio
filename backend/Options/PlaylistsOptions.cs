using Audio;

namespace Options;

public class PlaylistsOptions
{
    public required IReadOnlyDictionary<PlaylistType, IReadOnlyDictionary<string, string>> Urls { get; init; }
}