using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common;

public class MediaStorageOptions
{
    public string RootPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "media");
}

public interface IMediaStorage
{
    Task EnsureStorage();
    Task SaveAudio(long id, Stream stream);
    Task<IReadOnlyList<string>> GetImageKeys();
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

    public async Task<IReadOnlyList<string>> GetImageKeys()
    {
        await EnsureStorage();

        return Directory.EnumerateFiles(_imagesPath)
                        .Select(Path.GetFileName)
                        .OfType<string>()
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    public string GetAudioUrl(long id) => $"/api/radio/media/audio/{id}";

    public string GetImageUrl(string key) => $"/api/radio/media/images/{Uri.EscapeDataString(SafeFileName(key))}";

    public string GetAudioPath(long id) => Path.Combine(_audioPath, $"{id}{AudioExtension}");

    public string GetImagePath(string key) => Path.Combine(_imagesPath, SafeFileName(key));

    private static string SafeFileName(string key) => Path.GetFileName(key);
}
