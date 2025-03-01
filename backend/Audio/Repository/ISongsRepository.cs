namespace Audio;

public interface ISongsRepository
{
    IReadOnlyDictionary<PlaylistType, IReadOnlyList<SongMetadata>> Playlists { get; }
    
    Task Refresh();
}