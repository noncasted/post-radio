using System.Text.Json;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meta.Audio;

public interface IMetaDataCache
{
    string LookupFile { get; }
    int Count { get; }
    IReadOnlyDictionary<long, SongLookupInfo> Snapshot { get; }

    bool TryGet(long id, out SongLookupInfo? info);
    Task<bool> Reload(IOperationProgress? progress = null);
    void Replace(IReadOnlyDictionary<long, SongLookupInfo> data);
}

public class MetaDataCache : IMetaDataCache, ICoordinatorSetupCompleted
{
    public MetaDataCache(
        IOptions<AudioOptions> options,
        ILogger<MetaDataCache> logger)
    {
        _lookupFile = ResolveLookupFile(options.Value.SongLookupFile);
        _logger = logger;

        LoadFromDisk(progress: null, keepExistingOnFailure: true);
    }

    private const string MetadataDirectoryName = "metadata";
    private const string ToolsDirectoryName = "tools";
    private const string LookupFileName = "songs.json";

    private readonly object _lock = new();
    private readonly ILogger<MetaDataCache> _logger;
    private readonly string _lookupFile;
    private Dictionary<long, SongLookupInfo> _songs = new();

    public string LookupFile => _lookupFile;

    public int Count
    {
        get
        {
            lock (_lock)
                return _songs.Count;
        }
    }

    public IReadOnlyDictionary<long, SongLookupInfo> Snapshot
    {
        get
        {
            lock (_lock)
                return new Dictionary<long, SongLookupInfo>(_songs);
        }
    }

    public bool TryGet(long id, out SongLookupInfo? info)
    {
        lock (_lock)
            return _songs.TryGetValue(id, out info);
    }

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return Reload();
    }

    public Task<bool> Reload(IOperationProgress? progress = null)
    {
        return Task.FromResult(LoadFromDisk(progress, keepExistingOnFailure: true));
    }

    public void Replace(IReadOnlyDictionary<long, SongLookupInfo> data)
    {
        lock (_lock)
            _songs = new Dictionary<long, SongLookupInfo>(data);

        _logger.LogInformation("[Audio] [MetaDataCache] Replaced cache with {Count} song metadata entries", data.Count);
    }

    public static string ResolveLookupFile(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));

        var publishedPath = Path.Combine(AppContext.BaseDirectory, ToolsDirectoryName, MetadataDirectoryName, LookupFileName);

        if (File.Exists(publishedPath))
            return Path.GetFullPath(publishedPath);

        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);

        if (repositoryRoot != null)
            return Path.Combine(repositoryRoot, ToolsDirectoryName, MetadataDirectoryName, LookupFileName);

        return Path.GetFullPath(publishedPath);
    }

    private bool LoadFromDisk(IOperationProgress? progress, bool keepExistingOnFailure)
    {
        if (!File.Exists(_lookupFile))
        {
            progress?.Log($"Lookup file not found: {_lookupFile}");
            _logger.LogWarning("[Audio] [MetaDataCache] Lookup file not found: {File}", _lookupFile);
            return false;
        }

        try
        {
            progress?.Log($"Reading {_lookupFile}");
            var json = File.ReadAllText(_lookupFile);
            var data = JsonSerializer.Deserialize<Dictionary<long, SongLookupInfo>>(json) ?? new Dictionary<long, SongLookupInfo>();

            lock (_lock)
                _songs = data;

            _logger.LogInformation("[Audio] [MetaDataCache] Loaded {Count} song metadata entries from {File}", data.Count, _lookupFile);
            return true;
        }
        catch (Exception e)
        {
            progress?.Log($"Failed to read lookup file: {e.Message}");
            _logger.LogError(e, "[Audio] [MetaDataCache] Failed to load song metadata from {File}", _lookupFile);

            if (!keepExistingOnFailure)
            {
                lock (_lock)
                    _songs = new Dictionary<long, SongLookupInfo>();
            }

            return false;
        }
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory != null)
        {
            var toolsMetadataPath = Path.Combine(directory.FullName, ToolsDirectoryName, MetadataDirectoryName);
            var backendPath = Path.Combine(directory.FullName, "backend");

            if (Directory.Exists(toolsMetadataPath) && Directory.Exists(backendPath))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }
}
