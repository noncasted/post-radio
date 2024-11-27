using Extensions;
using Newtonsoft.Json;
using Options;
using SoundCloudExplode;
using SoundCloudExplode.Common;

namespace Audio;

public class SongsRepository : ISongsRepository
{
    public SongsRepository(SoundCloudClient soundCloud, PlaylistsOptions options)
    {
        _soundCloud = soundCloud;
        _options = options;
    }

    private const string PathPostfix = "-playlist-metadata.json";

    private readonly SoundCloudClient _soundCloud;
    private readonly PlaylistsOptions _options;
    private readonly List<SongMetadata> _tracks = new();
    private readonly Dictionary<string, SongMetadata> _shortNameToMetadata = new();

    public IReadOnlyList<SongMetadata> Tracks => _tracks;
    public IReadOnlyDictionary<string, SongMetadata> ShortNameToMetadata => _shortNameToMetadata;

    public async Task Refresh()
    {
        _tracks.Clear();
        
        foreach (var (name, _) in _options.Urls)
        {
            var path = $"{_options.PlaylistsPath}{name}{PathPostfix}";

            if (File.Exists(path) == false)
            {
                var tmp = JsonConvert.SerializeObject(new Dictionary<string, SongMetadata>());
                await File.WriteAllTextAsync(path, tmp);
            }

            var json = await File.ReadAllTextAsync(path);
            var metadata = JsonConvert.DeserializeObject<Dictionary<string, SongMetadata>>(json)!;

            var tracks = await _soundCloud.Playlists.GetTracksAsync(_options.Urls[name]);

            foreach (var track in tracks)
            {
                var data = track.ToMetadata();

                if (data == null)
                    continue;

                metadata.TryAdd(data.Url, data);
            }

            foreach (var (_, data) in metadata)
            {
                _tracks.Add(data);
                _shortNameToMetadata.Add(data.ShortName, data);
            }

            _tracks.Shuffle();
            
            var resultObject = JsonConvert.SerializeObject(metadata);

            await File.WriteAllTextAsync(path, resultObject);
            Console.WriteLine($"Refresh meta for: {name} path: {path}");
        }
    }
}