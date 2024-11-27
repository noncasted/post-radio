namespace Audio;

public interface IAudioAPI
{
    Task Refresh();
    Task<TrackData> GetNext(int index);
}