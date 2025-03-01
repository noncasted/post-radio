using System.Text;
using Extensions;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Newtonsoft.Json;
using Options;
using SoundCloudExplode;
using SoundCloudExplode.Common;

namespace Audio;

public class SongsRepository : ISongsRepository
{
    public SongsRepository(
        SoundCloudClient soundCloud,
        PlaylistsOptions playlistsOptions,
        MinioClient minio,
        MinioOptions minioOptions,
        ILogger<SongsRepository> logger)
    {
        _soundCloud = soundCloud;
        _playlistsOptions = playlistsOptions;
        _minio = minio;
        _minioOptions = minioOptions;
        _logger = logger;
    }

    private const string PathPostfix = "-playlist-metadata.json";

    private readonly SoundCloudClient _soundCloud;
    private readonly PlaylistsOptions _playlistsOptions;
    private readonly MinioClient _minio;
    private readonly MinioOptions _minioOptions;
    private readonly ILogger<SongsRepository> _logger;
    private readonly Dictionary<PlaylistType, IReadOnlyList<SongMetadata>> _tracks = new();
    private readonly Dictionary<string, SongMetadata> _shortNameToMetadata = new();

    public IReadOnlyDictionary<PlaylistType, IReadOnlyList<SongMetadata>> Playlists => _tracks;

    public async Task Refresh()
    {
        _logger.AudioRefreshStarted();

        _tracks.Clear();
        _shortNameToMetadata.Clear();

        foreach (var (type, options) in _playlistsOptions.Urls)
        {
            var playlistSongs = new List<SongMetadata>();
            _tracks.Add(type, playlistSongs);

            foreach (var (name, _) in options)
            {
                var json = string.Empty;
                var path = $"{name}{PathPostfix}";

                var getArgs = new GetObjectArgs()
                    .WithBucket(_minioOptions.AudioBucket)
                    .WithObject(path)
                    .WithCallbackStream(stream =>
                    {
                        using var reader = new StreamReader(stream);
                        json = reader.ReadToEnd();
                    });

                await _minio.GetObjectAsync(getArgs);

                var oldMetadata = JsonConvert.DeserializeObject<Dictionary<string, SongMetadata>>(json)!;
                var newMetadata = new Dictionary<string, SongMetadata>();

                foreach (var (_, data) in oldMetadata)
                    data.ShortName = data.Url.ToShortName();

                var link = _playlistsOptions.Urls[type][name];
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

                    playlistSongs.Add(data);
                    _shortNameToMetadata.Add(data.ShortName, data);
                }

                var resultObject = JsonConvert.SerializeObject(newMetadata, Formatting.Indented);

                var jsonBytes = Encoding.UTF8.GetBytes(resultObject);

                using var memoryStream = new MemoryStream(jsonBytes);

                await _minio.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_minioOptions.AudioBucket)
                    .WithObject(path)
                    .WithStreamData(memoryStream)
                    .WithObjectSize(memoryStream.Length)
                    .WithContentType("application/json")
                );
            }

            playlistSongs.Shuffle();
        }

        _logger.AudioRefreshCompleted(_tracks.Count);
    }
}