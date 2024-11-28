namespace Audio;

public class AudioAPI : IAudioAPI
{
    public AudioAPI(ISongsRepository repository, ISongProvider provider)
    {
        _repository = repository;
        _provider = provider;
    }

    private readonly ISongsRepository _repository;
    private readonly ISongProvider _provider;

    public Task Refresh()
    {
        return _repository.Refresh();
    }

    public Task<TrackData> GetNext(GetNextTrackRequest request)
    {
        return _provider.GetNext(request.Index);
    }
}