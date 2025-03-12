using Extensions;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Options;

namespace Images;

public class ImageRepository : IImageRepository
{
    public ImageRepository(
        MinioClient minio,
        MinioOptions options,
        ILogger<ImageRepository> logger)
    {
        _minio = minio;
        _options = options;
        _logger = logger;
    }

    private readonly MinioClient _minio;
    private readonly MinioOptions _options;
    private readonly ILogger<ImageRepository> _logger;

    private readonly List<string> _images = new();

    public async Task Run()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1));
            _images.Shuffle();
        }
    }

    public async Task Refresh()
    {
        _logger.ImageRefreshStarted();

        var listArgs = new ListObjectsArgs()
            .WithBucket(_options.ImagesBucket);

        var objects = _minio.ListObjectsEnumAsync(listArgs);

        _images.Clear();

        await foreach (var item in objects)
            _images.Add(item.Key);
        
        _images.Shuffle();

        _logger.ImageRefreshCompleted(_images.Count);
    }

    public async Task<ImageData> GetNext(int current)
    {
        current = (current + 1) % _images.Count;
        var key = _images[current];

        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(_options.ImagesBucket)
            .WithObject(key)
            .WithExpiry(50000);

        var signedUrl = await _minio.PresignedGetObjectAsync(presignedArgs);
        signedUrl = signedUrl.Replace("http://", "https://");

        return new ImageData()
        {
            Url = signedUrl
        };
    }
}