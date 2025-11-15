using Common;

namespace Audio;

public class Playlist : Grain, IPlaylist
{
    private readonly IPersistentState<PlaylistState> _state;

    public Playlist([States.Playlist] IPersistentState<PlaylistState> state)
    {
        _state = state;
    }

    public Task Setup(string url)
    {
        _state.State.Url = url;
        return _state.WriteStateAsync();
    }

    public Task SetName(string name)
    {
        _state.State.Name = name;
        return _state.WriteStateAsync();
    }

    public Task<PlaylistData> GetData()
    {
        return Task.FromResult(new PlaylistData
            {
                Id = this.GetPrimaryKey(),
                Url = _state.State.Url,
                Name = _state.State.Name
            }
        );
    }
}