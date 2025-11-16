using Infrastructure.Orleans;

namespace Audio;

public class PlaylistFactory : IPlaylistFactory
{
    public PlaylistFactory(IOrleans orleans, IPlaylistLoader loader, IPlaylistsCollection collection)
    {
        _orleans = orleans;
        _loader = loader;
        _collection = collection;
    }

    private readonly IPlaylistsCollection _collection;
    private readonly IPlaylistLoader _loader;

    private readonly IOrleans _orleans;

    public async Task Create(string url, string name)
    {
        var id = Guid.NewGuid();
        var grain = _orleans.GetGrain<IPlaylist>(id);

        await grain.Setup(url);
        await grain.SetName(name);
        await _collection.Refresh();
    }
}