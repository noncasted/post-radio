namespace Options;

public class PlaylistsOptions
{
    public required IReadOnlyDictionary<string, string> Urls { get; init; }
    public required string PlaylistsPath { get; init; }
}