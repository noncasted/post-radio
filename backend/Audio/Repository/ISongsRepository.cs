namespace Audio;

public interface ISongsRepository
{
    IReadOnlyList<SongMetadata> Tracks { get; }
    
    Task Refresh();
}