using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;

namespace Audio;

public interface ISongsCollection : IViewableDictionary<long, SongData>
{
    IReadOnlyDictionary<Guid, IReadOnlyList<SongData>> ByPlaylist { get; }

    Task Refresh();
    Task<string> GetUrl(SongData data);
}

public class SongsCollection : ViewableDictionary<long, SongData>, ISongsCollection, ICoordinatorSetupCompleted
{
    public SongsCollection(
        IOrleans orleans,
        IMessaging messaging,
        IObjectStorage objectStorage,
        ILogger<SongsCollection> logger)
    {
        _orleans = orleans;
        _messaging = messaging;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    private readonly Dictionary<Guid, IReadOnlyList<SongData>> _byPlaylist = new();
    private readonly ILogger<SongsCollection> _logger;
    private readonly IMessaging _messaging;
    private readonly IObjectStorage _objectStorage;

    private readonly IOrleans _orleans;
    private readonly MessageQueueId _refreshQueue = new("audio-songs-refresh");

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<RefreshSongsPayload>(lifetime, _refreshQueue, _ => OnRefreshRequested().NoAwait());
        return OnRefreshRequested();
    }

    public IReadOnlyDictionary<Guid, IReadOnlyList<SongData>> ByPlaylist => _byPlaylist;

    public Task Refresh()
    {
        return _messaging.PushDirectQueue(_refreshQueue, new RefreshSongsPayload());
    }

    public Task<string> GetUrl(SongData data)
    {
        return _objectStorage.GetUrl("audio", data.Id);
    }

    private async Task OnRefreshRequested()
    {
        Clear();
        _byPlaylist.Clear();

        _logger.LogInformation("[Audio] [Collection] [Songs] Refresh started");

        var reader = _orleans.CreateDbReader(States.Song)
            .SelectID()
            .SelectPayload()
            .WhereType(States.Song);

        var count = await reader.Count();
        var processed = 0;

        _logger.LogInformation("[Audio] [Collection] [Songs] Found {Count} songs", count);

        var query = reader.Read();

        await foreach (var entry in query)
        {
            var id = entry.Id1;
            var state = reader.Deserialize<SongState>(entry);

            this[id] = new SongData
            {
                Id = id,
                Playlists = state.Playlists,
                Url = state.Url,
                Author = state.Author,
                Name = state.Name,
                AddDate = state.AddDate
            };

            processed++;

            if (processed % 100 == 0)
                _logger.LogInformation(
                    "[Audio] [Collection] [Songs] Processed {Processed}/{Total} songs...",
                    processed,
                    count
                );
        }

        _logger.LogInformation("[Audio] [Collection] [Songs] Refresh completed");

        _byPlaylist.Clear();

        foreach (var song in Values)
        foreach (var playlistId in song.Playlists)
        {
            if (_byPlaylist.ContainsKey(playlistId) == false)
                _byPlaylist[playlistId] = new List<SongData>();

            ((List<SongData>)_byPlaylist[playlistId]).Add(song);
        }
    }
}

[GenerateSerializer]
public class RefreshSongsPayload
{
}