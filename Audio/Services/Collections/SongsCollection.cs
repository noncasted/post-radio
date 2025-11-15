using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using ServiceLoop;

namespace Audio;

public interface ISongsCollection : IViewableDictionary<long, SongData>
{
    IReadOnlyDictionary<Guid, IReadOnlyList<SongData>> ByPlaylist { get; }

    Task Refresh();
}

public class SongsCollection : ViewableDictionary<long, SongData>, ISongsCollection, ICoordinatorSetupCompleted
{
    public SongsCollection(IOrleans orleans, ILogger<SongsCollection> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<SongsCollection> _logger;
    private readonly Dictionary<Guid, IReadOnlyList<SongData>> _byPlaylist = new();

    public IReadOnlyDictionary<Guid, IReadOnlyList<SongData>> ByPlaylist => _byPlaylist;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return Refresh();
    }

    public async Task Refresh()
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
                AddDate = state.AddDate,
            };

            processed++;

            if (processed % 100 == 0)
            {
                _logger.LogInformation("[Audio] [Collection] [Songs] Processed {Processed}/{Total} songs...",
                    processed, count
                );
            }
        }

        _logger.LogInformation("[Audio] [Collection] [Songs] Refresh completed");

        _byPlaylist.Clear();

        foreach (var song in Values)
        {
            foreach (var playlistId in song.Playlists)
            {
                if (!_byPlaylist.ContainsKey(playlistId))
                    _byPlaylist[playlistId] = new List<SongData>();

                ((List<SongData>)_byPlaylist[playlistId]).Add(song);
            }
        }
    }
}