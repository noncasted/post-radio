namespace Audio;

public interface ISongProvider
{
    int GetCurrentIndex();
    Task<TrackData> GetNext(int current);
}