using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;

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
    public PlaylistsCollection(IOrleans orleans, IMessaging messaging, ILogger<PlaylistsCollection> logger)
    {
        _orleans = orleans;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<PlaylistsCollection> _logger;
    private readonly MessageQueueId _refreshQueue = new("audio-playlists-refresh");

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<RefreshPlaylistsPayload>(lifetime, _refreshQueue, _ => OnRefreshRequested().NoAwait());
        return OnRefreshRequested( );
    }

    public Task Refresh()
    {
        return _messaging.PushDirectQueue(_refreshQueue, new RefreshPlaylistsPayload());
    }

    private async Task OnRefreshRequested()
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


[GenerateSerializer]
public class RefreshPlaylistsPayload
{
}