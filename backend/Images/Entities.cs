using Audio;

namespace Images;

public class ImageData
{
    public required string Url { get; init; }
}

public class ImageRequest
{
    public required int Index { get; init; }
    public required PlaylistType TargetPlaylist { get; init; }
}