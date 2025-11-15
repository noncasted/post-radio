using Common;

namespace Audio;

public interface IPlaylistLoader
{
    Task Load(PlaylistData playlist, IOperationProgress progress);
}