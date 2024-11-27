using Minio;
using Minio.DataModel.Args;
using Options;
using SoundCloudExplode;

namespace Audio;

public class SongProvider : ISongProvider
{
    public SongProvider(
        ISongsRepository repository,
        MinioClient minio,
        SoundCloudClient soundCloud,
        HttpClient http,
        MinioOptions options)
    {
        _repository = repository;
        _minio = minio;
        _soundCloud = soundCloud;
        _http = http;
        _options = options;
    }

    private readonly ISongsRepository _repository;
    private readonly MinioClient _minio;
    private readonly SoundCloudClient _soundCloud;
    private readonly HttpClient _http;
    private readonly MinioOptions _options;

    private int _index = 0;

    private readonly Dictionary<string, string> _nameToUrl = new();

    public int GetCurrentIndex()
    {
        return _index;
    }

    public async Task<TrackData> GetNext(int current)
    {
        var nextIndex = current + 1;

        if (nextIndex >= _repository.Tracks.Count)
            nextIndex = 0;

        var metadata = _repository.Tracks[nextIndex];

        var url = await GetUrl();

        return new TrackData()
        {
            DownloadUrl = url,
            Index = nextIndex,
            Metadata = metadata
        };

        async Task<string> GetUrl()
        {
            if (_nameToUrl.TryGetValue(metadata.ShortName, out var downloadUrl) == true)
                return downloadUrl;

            try
            {
                return await ExtractUrl();
            }
            catch (Exception e)
            {
                await DownloadAsync(metadata);
                return await ExtractUrl();
            }

            async Task<string> ExtractUrl()
            {
                var presignedArgs = new PresignedGetObjectArgs()
                    .WithBucket(_options.AudioBucket)
                    .WithObject(metadata.ShortName);

                var signedUrl = await _minio.PresignedGetObjectAsync(presignedArgs);
                _nameToUrl.Add(metadata.ShortName, signedUrl);
                return signedUrl;
            }
        }
    }

    private async Task DownloadAsync(SongMetadata data)
    {
        var mp3TrackMediaUrl = await _soundCloud.Tracks.GetDownloadUrlAsync(data.Url)!;

        var destination = new MemoryStream();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, mp3TrackMediaUrl);

            var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode})." +
                    Environment.NewLine +
                    "Request:" +
                    Environment.NewLine +
                    request
                );
            }

            var totalLength = response.Content.Headers.ContentLength ?? 0;
            var stream = await response.Content.ReadAsStreamAsync();

            await stream.CopyToAsync(
                destination,
                (int)totalLength,
                new CancellationToken()
            );

            destination.Seek(0, SeekOrigin.Begin);

            await _minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_options.AudioBucket)
                .WithObject(data.ShortName)
                .WithStreamData(destination)
                .WithObjectSize(destination.Length)
                .WithContentType("audio/mpeg"));
        }
        finally
        {
            // Always dispose of the stream
            await destination.DisposeAsync();
        }
    }
}