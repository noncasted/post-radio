using Common;
using Infrastructure;
using Infrastructure.State;

namespace Meta.Audio;

public interface IPlaylist : IGrainWithGuidKey
{
    Task Setup(string url);
    Task SetName(string name);
    Task<PlaylistData> GetData();
}

[GenerateSerializer]
public class PlaylistData
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required string Url { get; init; }
    [Id(2)] public required string Name { get; init; }
}

[GenerateSerializer]
[GrainState(Table = "audio", State = "playlist", Lookup = "Playlist", Key = GrainKeyType.Guid)]
public class PlaylistState : IStateValue
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string Url { get; set; } = string.Empty;

    public int Version => 0;
}

public class Playlist : Grain, IPlaylist
{
    public Playlist(
        [State] State<PlaylistState> state,
        IStateCollection<Guid, PlaylistState> collection)
    {
        _state = state;
        _collection = collection;
    }

    private readonly State<PlaylistState> _state;
    private readonly IStateCollection<Guid, PlaylistState> _collection;

    public async Task Setup(string url)
    {
        var updated = await _state.Update(s => s.Url = url);
        await _collection.OnUpdated(this.GetPrimaryKey(), updated);
    }

    public async Task SetName(string name)
    {
        var updated = await _state.Update(s => s.Name = name);
        await _collection.OnUpdated(this.GetPrimaryKey(), updated);
    }

    public async Task<PlaylistData> GetData()
    {
        var state = await _state.ReadValue();

        return new PlaylistData
        {
            Id = this.GetPrimaryKey(),
            Url = state.Url,
            Name = state.Name
        };
    }
}