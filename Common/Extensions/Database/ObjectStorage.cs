using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Common;

public interface IObjectStorage
{
    Task<bool> Contains(string bucket, string key);
    Task<HashSet<string>> ContainsMany(string bucket, IEnumerable<string> keys);
    Task Put(string bucket, string key, MemoryStream stream, string type);
    Task<string> GetUrl(string bucket, string key);
    Task<IReadOnlyList<string>> GetAllKeys(string bucket);
}

public class ObjectStorage : IObjectStorage
{
    public ObjectStorage(IMinioClient client, ILogger<ObjectStorage> logger)
    {
        _client = client;
        _logger = logger;
    }

    private readonly IMinioClient _client;
    private readonly ILogger<ObjectStorage> _logger;

    public async Task<bool> Contains(string bucket, string key)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(key)
            );
        }
        catch
        {
            return false;
        }

        return true;
    }

    public async Task<HashSet<string>> ContainsMany(string bucket, IEnumerable<string> keys)
    {
        var existingKeys = new HashSet<string>();
        var keysSet = keys.ToHashSet();

        if (keysSet.Count == 0)
            return existingKeys;

        try
        {
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucket)
                .WithRecursive(true);

            var enumerable = _client.ListObjectsEnumAsync(listArgs);

            await foreach (var item in enumerable)
            {
                if (keysSet.Contains(item.Key))
                    existingKeys.Add(item.Key);

                // Early exit if we found all keys
                if (existingKeys.Count == keysSet.Count)
                    break;
            }
        }
        catch
        {
            // If listing fails, return empty set
            return existingKeys;
        }

        return existingKeys;
    }

    public async Task Put(string bucket, string key, MemoryStream stream, string type)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            
            var args = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(key)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(type);

            await _client.PutObjectAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ObjectStorage] Failed to upload object '{Key}' to bucket '{Bucket}'. Size: {Size} bytes. Exception type: {ExceptionType}",
                key, bucket, stream.Length, ex.GetType().Name
            );

            throw new InvalidOperationException(
                $"Failed to upload object '{key}' to bucket '{bucket}'. Size: {stream.Length} bytes",
                ex
            );
        }
    }

    public async Task<string> GetUrl(string bucket, string key)
    {
        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithExpiry(50000);

        var signedUrl = await _client.PresignedGetObjectAsync(presignedArgs);
        signedUrl = signedUrl.Replace("http://", "https://");

        return signedUrl;
    }

    public async Task<IReadOnlyList<string>> GetAllKeys(string bucket)
    {
        var listArgs = new ListObjectsArgs()
            .WithBucket("images");
        
        var objects = _client.ListObjectsEnumAsync(listArgs);

        var keys = new List<string>();

        await foreach (var item in objects)
            keys.Add(item.Key);

        return keys;
    }
}

public static class ObjectStorageExtensions
{
    public static Task<string> GetUrl(this IObjectStorage storage, string bucket, long id)
    {
        return storage.GetUrl(bucket, id.ToString());
    }
}