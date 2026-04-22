using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Common;

public interface IObjectStorage
{
    Task EnsureBucket(string bucket);
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

    public async Task EnsureBucket(string bucket)
    {
        try
        {
            var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket));

            if (!exists)
            {
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
                _logger.LogInformation("[ObjectStorage] Created bucket '{Bucket}'", bucket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ObjectStorage] Failed to ensure bucket '{Bucket}'", bucket);
        }
    }

    public async Task<bool> Contains(string bucket, string key)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(key));
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
            var listArgs = new ListObjectsArgs().WithBucket(bucket).WithRecursive(true);
            var enumerable = _client.ListObjectsEnumAsync(listArgs);

            await foreach (var item in enumerable)
            {
                if (keysSet.Contains(item.Key))
                    existingKeys.Add(item.Key);

                if (existingKeys.Count == keysSet.Count)
                    break;
            }
        }
        catch
        {
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
                "[ObjectStorage] Failed to upload '{Key}' to bucket '{Bucket}' ({Size} bytes)",
                key, bucket, stream.Length);

            throw new InvalidOperationException(
                $"Failed to upload object '{key}' to bucket '{bucket}' ({stream.Length} bytes)", ex);
        }
    }

    public async Task<string> GetUrl(string bucket, string key)
    {
        var args = new PresignedGetObjectArgs()
                   .WithBucket(bucket)
                   .WithObject(key)
                   .WithExpiry(50000);

        return await _client.PresignedGetObjectAsync(args);
    }

    public async Task<IReadOnlyList<string>> GetAllKeys(string bucket)
    {
        var keys = new List<string>();

        try
        {
            var listArgs = new ListObjectsArgs().WithBucket(bucket);
            var objects = _client.ListObjectsEnumAsync(listArgs);

            await foreach (var item in objects)
                keys.Add(item.Key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ObjectStorage] Failed to list bucket '{Bucket}'", bucket);
        }

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