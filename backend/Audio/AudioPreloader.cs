using Microsoft.Extensions.Hosting;

namespace Audio;

public interface IAudioPreloader
{
    Task Execute();
}

public class AudioPreloader : IAudioPreloader
{
    public AudioPreloader(ISongsRepository repository, ISongProvider provider)
    {
        _repository = repository;
        _provider = provider;
    }
 
    private readonly ISongsRepository _repository;
    private readonly ISongProvider _provider;

    public async Task Execute()
    {
        return;
        foreach (var (type, playlist) in _repository.Playlists)
        {
            for (var i = 0; i < playlist.Count; i++)
                await _provider.GetNext(i, type);
        } 
    }
}