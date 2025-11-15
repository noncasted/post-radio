namespace Audio;

public interface IPlaylist : IGrainWithGuidKey
{
    Task Setup(string url);
    Task SetName(string name);
    Task<PlaylistData> GetData();
}