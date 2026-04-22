using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Audio;

public interface IPlaylistsCollection : IStateCollection<Guid, PlaylistState>
{
}

public class PlaylistsCollection : StateCollection<Guid, PlaylistState>, IPlaylistsCollection
{
    public PlaylistsCollection(
        StateCollectionUtils<Guid, PlaylistState> utils,
        ILogger<StateCollection<Guid, PlaylistState>> logger)
        : base(utils, logger)
    {
    }
}