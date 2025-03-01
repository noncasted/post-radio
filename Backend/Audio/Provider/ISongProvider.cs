namespace Audio;

public interface ISongProvider
{
    Task<TrackData> GetNext(int current, PlaylistType playlist);
}