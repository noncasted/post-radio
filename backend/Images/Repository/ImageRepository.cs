using System.Text;
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

    private readonly List<ImageData> _images = new();

    public async Task Refresh()
    {
        _logger.ImageRefreshStarted();
        
        var listArgs = new ListObjectsArgs()
            .WithBucket(_options.ImagesBucket);

        var objects = _minio.ListObjectsEnumAsync(listArgs);

        _images.Clear();

        await foreach (var item in objects)
        {
            var presignedArgs = new PresignedGetObjectArgs()
                .WithBucket(_options.ImagesBucket)
                .WithObject(item.Key)
                .WithExpiry(50000);

            var signedUrl = await _minio.PresignedGetObjectAsync(presignedArgs);

            _images.Add(new ImageData()
            {
                Url = signedUrl
            });
        }
        
        _logger.ImageRefreshCompleted(_images.Count);
    }

    public ImageData GetNext(int current)
    {
        current = (current + 1) % _images.Count;
        return _images[current];
    }
}