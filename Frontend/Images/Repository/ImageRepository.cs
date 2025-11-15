using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Frontend;

public class ImageRepository : IImageRepository
{
    public ImageRepository(
        IMinioClient minio,
        ILogger<ImageRepository> logger)
    {
        _minio = minio;
        _logger = logger;
    }

    private readonly IMinioClient _minio;
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
            .WithBucket("images");

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
            .WithBucket("images")
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