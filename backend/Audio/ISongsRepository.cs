namespace Audio;

public interface ISongsRepository
{
    IReadOnlyList<SongMetadata> Tracks { get; }
    IReadOnlyDictionary<string, SongMetadata> ShortNameToMetadata { get; }
    
    Task Refresh();
}