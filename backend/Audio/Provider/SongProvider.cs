using System.Text;
using Microsoft.Extensions.Logging;
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
        MinioOptions options,
        ILogger<SongProvider> logger)
    {
        _repository = repository;
        _minio = minio;
        _soundCloud = soundCloud;
        _http = http;
        _options = options;
        _logger = logger;
    }

    private readonly ISongsRepository _repository;
    private readonly MinioClient _minio;
    private readonly SoundCloudClient _soundCloud;
    private readonly HttpClient _http;
    private readonly MinioOptions _options;
    private readonly ILogger<SongProvider> _logger;

    private readonly Dictionary<string, string> _nameToUrl = new();

    public async Task<TrackData> GetNext(int current)
    {
        current = (current + 1) % _repository.Tracks.Count;
        var metadata = _repository.Tracks[current];

        _logger.AudioGetStarted(current, metadata);

        var url = await GetUrl();

        return new TrackData()
        {
            DownloadUrl = url,
            Metadata = metadata
        };

        async Task<string> GetUrl()
        {
            if (_nameToUrl.TryGetValue(metadata.ShortName, out var downloadUrl) == true)
            {
                _logger.AudioAlreadyCached(current, metadata);
                return downloadUrl;
            }

            try
            {
                await _minio.StatObjectAsync(new StatObjectArgs()
                    .WithBucket(_options.AudioBucket)
                    .WithObject(metadata.ShortName));

                _logger.AudioFound(metadata);
            }
            catch
            {
                _logger.AudioNotLoaded(metadata);
                await DownloadAsync(metadata);
            }

            return await ExtractUrl();

            async Task<string> ExtractUrl()
            {
                var presignedArgs = new PresignedGetObjectArgs()
                    .WithBucket(_options.AudioBucket)
                    .WithObject(metadata.ShortName)
                    .WithExpiry(50000);

                var signedUrl = await _minio.PresignedGetObjectAsync(presignedArgs);
                signedUrl = signedUrl.Replace("http://", "https://");
                
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
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            // Always dispose of the stream
            await destination.DisposeAsync();
        }
    }
}