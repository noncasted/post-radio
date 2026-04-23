using Common;
using Infrastructure;
using Infrastructure.State;

namespace Meta.Audio;

public interface ISong : IGrainWithIntegerKey
{
    Task UpdateData(SongData data);
    Task<SongData> GetData();
    Task AddToPlaylist(Guid playlistId);
    Task RemoveFromPlaylist(Guid playlistId);
    Task SetLoaded(bool loaded);
    Task SetAudioData(bool loaded, long? durationMs, bool isValid);
    Task SetValid(bool isValid);
}

[GenerateSerializer]
public class SongData
{
    [Id(0)] public required long Id { get; init; }
    [Id(1)] public required IReadOnlyList<Guid> Playlists { get; init; }
    [Id(2)] public required string Url { get; init; }
    [Id(3)] public required string Author { get; init; }
    [Id(4)] public required string Name { get; init; }
    [Id(5)] public required DateTime AddDate { get; init; }
    [Id(6)] public required bool IsLoaded { get; init; }
    [Id(7)] public long? DurationMs { get; init; }
    [Id(8)] public required bool IsValid { get; init; }
}

[GenerateSerializer]
[GrainState(Table = "songs", State = "song", Lookup = "Song", Key = GrainKeyType.Integer)]
public class SongState : IStateValue
{
    [Id(0)] public string Url { get; set; } = string.Empty;
    [Id(1)] public List<Guid> Playlists { get; set; } = new();
    [Id(2)] public string Author { get; set; } = string.Empty;
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public DateTime AddDate { get; set; }
    [Id(5)] public bool IsLoaded { get; set; }
    [Id(6)] public long? DurationMs { get; set; }
    [Id(7)] public bool IsValid { get; set; } = true;

    public int Version => 0;
}

public class Song : Grain, ISong
{
    public Song(
        [State] State<SongState> state,
        IStateCollection<long, SongState> collection)
    {
        _state = state;
        _collection = collection;
    }

    private readonly State<SongState> _state;
    private readonly IStateCollection<long, SongState> _collection;

    public async Task UpdateData(SongData data)
    {
        var updated = await _state.Update(s => {
            s.Url = data.Url;
            s.Author = data.Author;
            s.Name = data.Name;
            s.Playlists = data.Playlists.ToList();
            s.AddDate = data.AddDate;
            s.IsLoaded = data.IsLoaded;
            s.DurationMs = data.DurationMs;
            s.IsValid = data.IsValid;
        });

        await _collection.OnUpdated(this.GetPrimaryKeyLong(), updated);
    }

    public async Task<SongData> GetData()
    {
        var state = await _state.ReadValue();

        return new SongData
        {
            Id = this.GetPrimaryKeyLong(),
            Url = state.Url,
            Author = state.Author,
            Name = state.Name,
            Playlists = state.Playlists,
            AddDate = state.AddDate,
            IsLoaded = state.IsLoaded,
            DurationMs = state.DurationMs,
            IsValid = state.IsValid
        };
    }

    public async Task SetLoaded(bool loaded)
    {
        var updated = await _state.Update(s => s.IsLoaded = loaded);
        await _collection.OnUpdated(this.GetPrimaryKeyLong(), updated);
    }

    public async Task SetAudioData(bool loaded, long? durationMs, bool isValid)
    {
        var updated = await _state.Update(s => {
            s.IsLoaded = loaded;
            s.DurationMs = durationMs;
            s.IsValid = isValid;
        });
        await _collection.OnUpdated(this.GetPrimaryKeyLong(), updated);
    }

    public async Task SetValid(bool isValid)
    {
        var updated = await _state.Update(s => {
            s.IsValid = isValid;
        });
        await _collection.OnUpdated(this.GetPrimaryKeyLong(), updated);
    }

    public async Task AddToPlaylist(Guid playlistId)
    {
        var updated = await _state.Update(s => {
            if (s.Playlists.Contains(playlistId))
                return;

            s.Playlists.Add(playlistId);
            s.AddDate = DateTime.UtcNow;
        });

        await _collection.OnUpdated(this.GetPrimaryKeyLong(), updated);
    }

    public async Task RemoveFromPlaylist(Guid playlistId)
    {
        var updated = await _state.Update(s => s.Playlists.Remove(playlistId));
        await _collection.OnUpdated(this.GetPrimaryKeyLong(), updated);
    }
}
