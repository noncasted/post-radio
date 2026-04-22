using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Audio;

public interface ISongsCollection : IStateCollection<long, SongState>
{
    IReadOnlyDictionary<Guid, IReadOnlyList<(long Id, SongState Song)>> ByPlaylist { get; }
}

public class SongsCollection : StateCollection<long, SongState>, ISongsCollection
{
    public SongsCollection(
        StateCollectionUtils<long, SongState> utils,
        ILogger<StateCollection<long, SongState>> logger)
        : base(utils, logger)
    {
    }

    public IReadOnlyDictionary<Guid, IReadOnlyList<(long Id, SongState Song)>> ByPlaylist
    {
        get
        {
            var result = new Dictionary<Guid, List<(long, SongState)>>();

            foreach (var (id, song) in this)
                foreach (var playlistId in song.Playlists)
                {
                    if (!result.TryGetValue(playlistId, out var list))
                        result[playlistId] = list = new List<(long, SongState)>();

                    list.Add((id, song));
                }

            return result.ToDictionary(kv => kv.Key,
                kv => (IReadOnlyList<(long, SongState)>)kv.Value);
        }
    }
}