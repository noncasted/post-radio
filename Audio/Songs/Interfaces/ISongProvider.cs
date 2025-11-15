namespace Audio;

public interface ISongProvider
{
    Task<string> GetUrl(SongData data);
}