using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common;

public class MediaStorageOptions
{
    public string RootPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "media");
}

public sealed record MediaImage(string Key, string FileName, long SizeBytes, DateTime LastModifiedUtc);

public interface IMediaStorage
{
    Task EnsureStorage();
    Task SaveAudio(long id, Stream stream);
    Task<IReadOnlyList<MediaImage>> GetImages();
    Task<IReadOnlyList<string>> GetImageKeys();
    Task<MediaImage> SaveImage(string fileName, Stream stream);
    Task<bool> DeleteImage(string key);
    Task WriteImagesArchive(Stream stream);
    string GetAudioUrl(long id);
    string GetImageUrl(string key);
    string GetAudioPath(long id);
    string GetImagePath(string key);
}

public class MediaStorage : IMediaStorage
{
    public MediaStorage(IOptions<MediaStorageOptions> options, ILogger<MediaStorage> logger)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        _audioPath = Path.Combine(_rootPath, AudioDirectoryName);
        _imagesPath = Path.Combine(_rootPath, ImagesDirectoryName);
        _logger = logger;
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif",
        ".bmp",
        ".gif",
        ".heic",
        ".jpeg",
        ".jpg",
        ".png",
        ".svg",
        ".webp"
    };

    private const string AudioDirectoryName = "audio";
    private const string ImagesDirectoryName = "images";
    private const string AudioExtension = ".mp3";

    private readonly string _audioPath;
    private readonly string _imagesPath;
    private readonly ILogger<MediaStorage> _logger;
    private readonly string _rootPath;
    private bool _loggedRootPath;

    public Task EnsureStorage()
    {
        Directory.CreateDirectory(_audioPath);
        Directory.CreateDirectory(_imagesPath);

        if (!_loggedRootPath)
        {
            _logger.LogInformation("[MediaStorage] Root directory: {RootPath}", _rootPath);
            _loggedRootPath = true;
        }

        return Task.CompletedTask;
    }

    public async Task SaveAudio(long id, Stream stream)
    {
        await EnsureStorage();

        var path = GetAudioPath(id);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read,
            bufferSize: 1024 * 128, useAsync: true);
        await stream.CopyToAsync(file);
    }

    public async Task<IReadOnlyList<MediaImage>> GetImages()
    {
        await EnsureStorage();

        return Directory.EnumerateFiles(_imagesPath)
                        .Where(IsSupportedImagePath)
                        .Select(ToMediaImage)
                        .OrderBy(image => image.FileName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    public async Task<IReadOnlyList<string>> GetImageKeys()
    {
        var images = await GetImages();
        return images.Select(image => image.Key).ToList();
    }

    public async Task<MediaImage> SaveImage(string fileName, Stream stream)
    {
        await EnsureStorage();

        var safeFileName = NormalizeImageFileName(fileName);
        var path = GetAvailableImagePath(safeFileName);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            bufferSize: 1024 * 128, useAsync: true);
        await stream.CopyToAsync(file);

        return ToMediaImage(path);
    }

    public async Task<bool> DeleteImage(string key)
    {
        await EnsureStorage();

        var path = GetImagePath(key);
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public async Task WriteImagesArchive(Stream stream)
    {
        var images = await GetImages();

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var image in images)
        {
            var path = GetImagePath(image.Key);
            if (!File.Exists(path))
                continue;

            var entry = archive.CreateEntry(image.FileName, CompressionLevel.Fastest);
            await using var entryStream = entry.Open();
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 1024 * 128, useAsync: true);
            await file.CopyToAsync(entryStream);
        }
    }

    public string GetAudioUrl(long id) => $"/api/radio/media/audio/{id}";

    public string GetImageUrl(string key) => $"/api/radio/media/images/{Uri.EscapeDataString(SafeFileName(key))}";

    public string GetAudioPath(long id) => Path.Combine(_audioPath, $"{id}{AudioExtension}");

    public string GetImagePath(string key) => Path.Combine(_imagesPath, SafeFileName(key));

    private static bool IsSupportedImagePath(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    private string GetAvailableImagePath(string safeFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        var path = Path.Combine(_imagesPath, safeFileName);

        for (var suffix = 1; File.Exists(path); suffix++)
            path = Path.Combine(_imagesPath, $"{baseName}-{suffix}{extension}");

        return path;
    }

    private static string NormalizeImageFileName(string fileName)
    {
        var safeFileName = SafeFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            throw new InvalidOperationException("Image file name is empty.");

        if (!IsSupportedImagePath(safeFileName))
            throw new InvalidOperationException($"Unsupported image file type: {safeFileName}");

        return safeFileName;
    }

    private static string SafeFileName(string key) => Path.GetFileName(key);

    private static MediaImage ToMediaImage(string path)
    {
        var info = new FileInfo(path);
        return new MediaImage(info.Name, info.Name, info.Length, info.LastWriteTimeUtc);
    }
}
