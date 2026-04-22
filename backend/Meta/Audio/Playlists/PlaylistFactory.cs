using Infrastructure;

namespace Meta.Audio;

public interface IPlaylistFactory
{
    Task<Guid> Create(string url, string name);
}

public class PlaylistFactory : IPlaylistFactory
{
    public PlaylistFactory(IOrleans orleans)
    {
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;

    public async Task<Guid> Create(string url, string name)
    {
        var id = Guid.NewGuid();
        var grain = _orleans.GetGrain<IPlaylist>(id);

        await grain.Setup(url);
        await grain.SetName(name);

        return id;
    }
}