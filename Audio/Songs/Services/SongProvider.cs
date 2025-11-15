using Common;

namespace Audio;

public class SongProvider : ISongProvider
{
    public SongProvider(IObjectStorage objectStorage)
    {
        _objectStorage = objectStorage;
    }

    private readonly IObjectStorage _objectStorage;

    public Task<string> GetUrl(SongData data)
    {
        return _objectStorage.GetUrl("audio", data.Id);
    }
}