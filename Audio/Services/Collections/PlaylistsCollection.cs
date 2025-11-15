using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using ServiceLoop;

namespace Audio;

public interface IPlaylistsCollection : IViewableDictionary<Guid, PlaylistData>
{
    Task Refresh();
}

public class PlaylistsCollection :
    ViewableDictionary<Guid, PlaylistData>,
    IPlaylistsCollection,
    ICoordinatorSetupCompleted
{
    private readonly IOrleans _orleans;
    private readonly ILogger<PlaylistsCollection> _logger;

    public PlaylistsCollection(IOrleans orleans, ILogger<PlaylistsCollection> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return Refresh();
    }

    public async Task Refresh()
    {
        _logger.LogInformation("[Audio] [Collection] [Playlists] Refresh started");

        var reader = _orleans.CreateDbReader(States.Playlist)
            .SelectID()
            .SelectPayload()
            .WhereType(States.Playlist);
        
        var query = reader.Read();

        await foreach (var entry in query)
        {
            var id = entry.GetId();
            var state = reader.Deserialize<PlaylistState>(entry);

            this[id] = new PlaylistData
            {
                Id = id,
                Url = state.Url,
                Name = state.Name
            };
        }

        _logger.LogInformation("[Audio] [Collection] [Playlists] Refresh completed with {Count} playlists", Count);
    }
}