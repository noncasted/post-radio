using Common;

namespace Audio;

public class Song : Grain, ISong
{
    public Song([States.Song] IPersistentState<SongState> state)
    {
        _state = state;
    }

    private readonly IPersistentState<SongState> _state;

    public Task UpdateData(SongData data)
    {
        var state = _state.State;

        state.Author = data.Author;
        state.Name = data.Name;
        state.Url = data.Url;
        state.Playlists = data.Playlists.ToList();
        state.AddDate = data.AddDate;
        return _state.WriteStateAsync();
    }

    public Task<SongData> GetData()
    {
        var id = this.GetPrimaryKeyLong();

        var state = _state.State;

        var data = new SongData
        {
            Id = id,
            Author = state.Author,
            Name = state.Name,
            Url = state.Url,
            Playlists = state.Playlists,
            AddDate = state.AddDate
        };

        return Task.FromResult(data);
    }

    public Task AddToPlaylist(Guid playlistId)
    {
        var state = _state.State;

        if (state.Playlists.Contains(playlistId) == false)
        {
            state.Playlists.Add(playlistId);
            state.AddDate = DateTime.UtcNow;
            return _state.WriteStateAsync();
        }

        return Task.CompletedTask;
    }

    public Task RemoveFromPlaylist(Guid playlistId)
    {
        var state = _state.State;

        if (state.Playlists.Contains(playlistId))
        {
            state.Playlists.Remove(playlistId);
            return _state.WriteStateAsync();
        }

        return Task.CompletedTask;
    }
}