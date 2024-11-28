using Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Options;
using SoundCloudExplode;
using SoundCloudExplode.Common;

namespace Audio;

public class SongsRepository : ISongsRepository
{
    public SongsRepository(
        SoundCloudClient soundCloud,
        PlaylistsOptions options,
        ILogger<SongsRepository> logger)
    {
        _soundCloud = soundCloud;
        _options = options;
        _logger = logger;
    }

    private const string PathPostfix = "-playlist-metadata.json";

    private readonly SoundCloudClient _soundCloud;
    private readonly PlaylistsOptions _options;
    private readonly ILogger<SongsRepository> _logger;
    private readonly List<SongMetadata> _tracks = new();
    private readonly Dictionary<string, SongMetadata> _shortNameToMetadata = new();

    public IReadOnlyList<SongMetadata> Tracks => _tracks;

    public async Task Refresh()
    {
        _logger.AudioRefreshStarted();

        _tracks.Clear();
        _shortNameToMetadata.Clear();

        foreach (var (name, _) in _options.Urls)
        {
            var path = $"{_options.PlaylistsPath}{name}{PathPostfix}";

            if (File.Exists(path) == false)
            {
                var tmp = JsonConvert.SerializeObject(new Dictionary<string, SongMetadata>());
                await File.WriteAllTextAsync(path, tmp);
            }

            var json = await File.ReadAllTextAsync(path);
            var oldMetadata = JsonConvert.DeserializeObject<Dictionary<string, SongMetadata>>(json)!;
            var newMetadata = new Dictionary<string, SongMetadata>();

            foreach (var (_, data) in oldMetadata)
                data.ShortName = data.Url.ToShortName();

            var link = _options.Urls[name];
            var tracks = await _soundCloud.Playlists.GetTracksAsync(link);

            foreach (var track in tracks)
            {
                var data = track.ToMetadata();

                if (data == null)
                    continue;

                if (oldMetadata.TryGetValue(data.Url, out var value) == true)
                    newMetadata.TryAdd(data.Url, value);
                else
                    newMetadata.TryAdd(data.Url, data);
            }

            foreach (var (_, data) in newMetadata)
            {
                if (_shortNameToMetadata.ContainsKey(data.ShortName) == true)
                    continue;

                _tracks.Add(data);
                _shortNameToMetadata.Add(data.ShortName, data);
            }

            _tracks.Shuffle();

            var resultObject = JsonConvert.SerializeObject(newMetadata, Formatting.Indented);

            await File.WriteAllTextAsync(path, resultObject);
        }

        _logger.AudioRefreshCompleted(_tracks.Count);
    }
}