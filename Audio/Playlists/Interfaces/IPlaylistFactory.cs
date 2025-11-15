namespace Audio;

public interface IPlaylistFactory
{
    Task Create(string url, string name);
}